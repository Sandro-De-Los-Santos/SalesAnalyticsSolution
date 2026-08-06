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
        _logger.LogInformation("Iniciando proceso ETL (Staging -> ODS -> DataWarehouse): {time}", DateTimeOffset.Now);

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
            // 1. EXTRACCION DE ARCHIVOS CSV
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

            // 2. PROCESAMIENTO ODS - CLIENTES
            _logger.LogInformation("Procesando clientes ODS...");
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
            _logger.LogInformation("Clientes ODS -> insertados: {ins}, rechazados: {rej}",
                clientesInsertados, clientesRechazados);

            // 3. PROCESAMIENTO ODS - PRODUCTOS
            _logger.LogInformation("Procesando productos ODS...");
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
            _logger.LogInformation("Productos ODS -> insertados: {ins}, rechazados: {rej}",
                productosInsertados, productosRechazados);

            // 4. PROCESAMIENTO ODS - VENTAS
            _logger.LogInformation("Procesando ventas ODS...");
            var ventas = VentaTransformer.TransformarTodas(
                ordenesCsv, detallesCsv, clientesValidos, productosValidos, idFuenteVentas,
                out int rechazadosCancelado, out int rechazadosReferencia);

            int ventasInsertadas = 0;
            foreach (var venta in ventas)
            { _repo.InsertVenta(venta); ventasInsertadas++; }

            totalProcesados += detallesCsv.Count;
            totalInsertados += ventasInsertadas;
            totalRechazados += (rechazadosCancelado + rechazadosReferencia);
            _logger.LogInformation("Ventas ODS -> insertadas: {ins}, rechazadas: {rej}",
                ventasInsertadas, rechazadosCancelado + rechazadosReferencia);

            // 5. CARGA DE DIMENSIONES Y TABLA DE HECHOS AL DATA WAREHOUSE (DW)
            _logger.LogInformation("Iniciando carga de Dimensiones al Data Warehouse...");
            CargarDataWarehouse();

            _repo.ActualizarLogFin(idLog, totalProcesados, totalInsertados, totalRechazados, "COMPLETADO");
            _logger.LogInformation("Proceso ETL finalizado con éxito. Total procesados: {p}, insertados: {i}, rechazados: {r}",
                totalProcesados, totalInsertados, totalRechazados);
        }
        catch (Exception ex)
        {
            _repo.ActualizarLogFin(idLog, totalProcesados, totalInsertados, totalRechazados, "ERROR", ex.Message);
            _logger.LogError(ex, "Error durante el proceso ETL");
        }
    }

    private void CargarDataWarehouse()
    {
        // 5.1 Carga DimFuenteDatos
        var fuentes = _repo.ObtenerFuentesDatos();
        foreach (var f in fuentes)
        {
            string nombreTipo = _repo.GetNombreTipoFuente(f.IdTipoFuente);
            var dimFuente = DimensionTransformer.TransformarDimFuente(f, nombreTipo);
            _repo.UpsertDimFuenteDatos(dimFuente);
        }
        _logger.LogInformation("DimFuenteDatos cargada.");

        // 5.2 Carga DimCliente
        var clientes = _repo.ObtenerClientes();
        foreach (var c in clientes)
        {
            var dimCliente = DimensionTransformer.TransformarDimCliente(c);
            _repo.UpsertDimCliente(dimCliente);
        }
        _logger.LogInformation("DimCliente cargada.");

        // 5.3 Carga DimProducto
        var productos = _repo.ObtenerProductos();
        foreach (var p in productos)
        {
            string nombreCat = _repo.GetNombreCategoria(p.IdCategoria.GetValueOrDefault());
            var dimProducto = DimensionTransformer.TransformarDimProducto(p, nombreCat);
            _repo.UpsertDimProducto(dimProducto);
        }
        _logger.LogInformation("DimProducto cargada.");

        // 5.4 Carga DimTiempo & FactVentas
        var ventas = _repo.ObtenerVentas();
        foreach (var v in ventas)
        {
            // DimTiempo
            var dimTiempo = DimensionTransformer.TransformarDimTiempo(v.Fecha);
            _repo.UpsertDimTiempo(dimTiempo);

            // Lookup Keys
            int clienteKey = _repo.GetClienteKeyByOrigen(v.IdCliente);
            int productoKey = _repo.GetProductoKeyByOrigen(v.IdProducto);
            int fuenteKey = _repo.GetFuenteKeyByOrigen(fuentes.FirstOrDefault()?.IdFuente ?? 1);
            int tiempoKey = dimTiempo.TiempoKey;

            if (clienteKey > 0 && productoKey > 0)
            {
                var factVenta = DimensionTransformer.CrearFactVenta(v, clienteKey, productoKey, fuenteKey, tiempoKey);
                _repo.InsertFactVentas(factVenta);
            }
        }
        _logger.LogInformation("DimTiempo y FactVentas cargadas exitosamente.");
    }
}