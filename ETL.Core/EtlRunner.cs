using ETL.Core.Data;
using ETL.Core.Extract;
using ETL.Core.Models;
using ETL.Core.Transform;
using Microsoft.Extensions.Logging;

namespace ETL.Core;

public class EtlRunner
{
    private readonly Repository _repo;
    private readonly ILogger<EtlRunner> _logger;
    private readonly string _csvBasePath;
    private readonly string _connectionString;

    public EtlRunner(ILogger<EtlRunner> logger, string csvBasePath, string connectionString)
    {
        _logger = logger;
        _csvBasePath = csvBasePath;
        _connectionString = connectionString;
        _repo = new Repository(connectionString);
    }

    public void Ejecutar()
    {
        _logger.LogInformation("Iniciando proceso ETL: {time}", DateTimeOffset.Now);

        
        int idTipoFuenteCsv = _repo.BuscarTipoFuentePorNombre("CSV")
            ?? _repo.InsertTipoFuente(new TipoFuente { Nombre = "CSV", Descripcion = "Archivos planos CSV" });

        int idFuenteClientes = _repo.InsertFuenteDatos(new FuenteDatos
        { NombreFuente = "customers.csv", Descripcion = "Clientes", FechaRegistro = DateTime.Now, IdTipoFuente = idTipoFuenteCsv });

        int idFuenteProductos = _repo.InsertFuenteDatos(new FuenteDatos
        { NombreFuente = "products.csv", Descripcion = "Productos", FechaRegistro = DateTime.Now, IdTipoFuente = idTipoFuenteCsv });

        int idFuenteVentas = _repo.InsertFuenteDatos(new FuenteDatos
        { NombreFuente = "orders.csv + order_details.csv", Descripcion = "Ventas", FechaRegistro = DateTime.Now, IdTipoFuente = idTipoFuenteCsv });

        int idLog = _repo.InsertLogInicio(idFuenteClientes);
        int totalProcesados = 0, totalInsertados = 0, totalRechazados = 0;

        try
        {
           
            var csvClientes = new CsvExtractor<CustomerCsv>(
                Path.Combine(_csvBasePath, "customers.csv"), _logger);
            var csvProductos = new CsvExtractor<ProductCsv>(
                Path.Combine(_csvBasePath, "products.csv"), _logger);
            var csvOrdenes = new CsvExtractor<OrderCsv>(
                Path.Combine(_csvBasePath, "orders.csv"), _logger);
            var csvDetalles = new CsvExtractor<OrderDetailCsv>(
                Path.Combine(_csvBasePath, "order_details.csv"), _logger);

            var clientesCsv = csvClientes.ExtractAsync().GetAwaiter().GetResult();
            var productosCsv = csvProductos.ExtractAsync().GetAwaiter().GetResult();
            var ordenesCsv = csvOrdenes.ExtractAsync().GetAwaiter().GetResult();
            var detallesCsv = csvDetalles.ExtractAsync().GetAwaiter().GetResult();

            
            _logger.LogInformation("Procesando clientes...");
            var clientesValidos = new HashSet<int>();
            int clientesInsertados = 0, clientesRechazados = 0;

            foreach (var c in clientesCsv)
            {
                totalProcesados++;
                var cliente = ClienteTransformer.Transformar(c, idFuenteClientes);
                if (cliente == null || _repo.ExisteCliente(cliente.IdCliente) ||
                    (cliente.Email != null && _repo.ExisteEmailCliente(cliente.Email)))
                { clientesRechazados++; continue; }

                _repo.InsertCliente(cliente);
                clientesValidos.Add(cliente.IdCliente);
                clientesInsertados++;
            }
            totalInsertados += clientesInsertados;
            totalRechazados += clientesRechazados;
            _logger.LogInformation("Clientes -> insertados: {ins}, rechazados: {rej}",
                clientesInsertados, clientesRechazados);

            
            _logger.LogInformation("Procesando productos...");
            var productoTransformer = new ProductoTransformer(_repo);
            var productosValidos = new HashSet<int>();
            int productosInsertados = 0, productosRechazados = 0;

            foreach (var p in productosCsv)
            {
                totalProcesados++;
                var producto = productoTransformer.Transformar(p, idFuenteProductos);
                if (producto == null || _repo.ExisteProducto(producto.IdProducto))
                { productosRechazados++; continue; }

                _repo.InsertProducto(producto);
                productosValidos.Add(producto.IdProducto);
                productosInsertados++;
            }
            totalInsertados += productosInsertados;
            totalRechazados += productosRechazados;
            _logger.LogInformation("Productos -> insertados: {ins}, rechazados: {rej}",
                productosInsertados, productosRechazados);

            
            _logger.LogInformation("Procesando ventas...");
            var ventas = VentaTransformer.TransformarTodas(
                ordenesCsv, detallesCsv, clientesValidos, productosValidos, idFuenteVentas,
                out int rechazadosCancelado, out int rechazadosReferencia);

            int ventasInsertadas = 0;
            foreach (var venta in ventas)
            { _repo.InsertVenta(venta); ventasInsertadas++; }

            totalProcesados += detallesCsv.Count;
            totalInsertados += ventasInsertadas;
            totalRechazados += (rechazadosCancelado + rechazadosReferencia);
            _logger.LogInformation("Ventas -> insertadas: {ins}, rechazadas: {rej}",
                ventasInsertadas, rechazadosCancelado + rechazadosReferencia);

            
            _repo.ActualizarLogFin(idLog, totalProcesados, totalInsertados, totalRechazados, "COMPLETADO");
            _logger.LogInformation("Proceso ETL finalizado. Total procesados: {p}, insertados: {i}, rechazados: {r}",
                totalProcesados, totalInsertados, totalRechazados);
        }
        catch (Exception ex)
        {
            _repo.ActualizarLogFin(idLog, totalProcesados, totalInsertados, totalRechazados, "ERROR", ex.Message);
            _logger.LogError(ex, "Error durante el proceso ETL");
        }
    }
}