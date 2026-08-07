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

    /// <summary>
    /// Carga las dimensiones y tabla de hechos en VentasDW leyendo los datos ya existentes en AnalyticDB.
    /// </summary>
    public void CargarSoloDataWarehouse()
    {
        _logger.LogInformation("Iniciando Carga de Dimensiones al Data Warehouse (VentasDW) desde AnalyticDB: {time}", DateTimeOffset.Now);

        try
        {
            CargarDataWarehouse();
            _logger.LogInformation("Carga al Data Warehouse (VentasDW) completada exitosamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante la carga al Data Warehouse (VentasDW)");
            throw;
        }
    }

    public void Ejecutar()
    {
        _logger.LogInformation("Iniciando proceso completo ETL (AnalyticDB -> VentasDW): {time}", DateTimeOffset.Now);

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
            // Carga de dimensiones en VentasDW usando la data relacional de AnalyticDB
            CargarDataWarehouse();

            _repo.ActualizarLogFin(idLog, totalProcesados, totalInsertados, totalRechazados, "COMPLETADO");
            _logger.LogInformation("Proceso ETL finalizado con éxito.");
        }
        catch (Exception ex)
        {
            _repo.ActualizarLogFin(idLog, totalProcesados, totalInsertados, totalRechazados, "ERROR", ex.Message);
            _logger.LogError(ex, "Error durante la ejecución del proceso ETL");
        }
    }

    private void CargarDataWarehouse()
    {
        // 1. Carga Dim_Fuente
        var fuentes = _repo.ObtenerFuentesDatos();
        foreach (var f in fuentes)
        {
            string nombreTipo = _repo.GetNombreTipoFuente(f.IdTipoFuente);
            var dimFuente = DimensionTransformer.TransformarDimFuente(f, nombreTipo);
            _repo.UpsertDimFuenteDatos(dimFuente);
        }
        _logger.LogInformation("Dim_Fuente cargada.");

        // 2. Carga Dim_Cliente
        var clientes = _repo.ObtenerClientes();
        foreach (var c in clientes)
        {
            var dimCliente = DimensionTransformer.TransformarDimCliente(c);
            _repo.UpsertDimCliente(dimCliente);
        }
        _logger.LogInformation("Dim_Cliente cargada.");

        // 3. Carga Dim_Producto
        var productos = _repo.ObtenerProductos();
        foreach (var p in productos)
        {
            string nombreCat = _repo.GetNombreCategoria(p.IdCategoria.GetValueOrDefault());
            var dimProducto = DimensionTransformer.TransformarDimProducto(p, nombreCat);
            _repo.UpsertDimProducto(dimProducto);
        }
        _logger.LogInformation("Dim_Producto cargada.");

        // 4. Carga Dim_Tiempo y Fact_Ventas
        var ventas = _repo.ObtenerVentas();
        foreach (var v in ventas)
        {
            // Dim_Tiempo
            var dimTiempo = DimensionTransformer.TransformarDimTiempo(v.Fecha);
            _repo.UpsertDimTiempo(dimTiempo);

            // Lookup Keys
            int clienteKey = _repo.GetClienteKeyByOrigen(v.IdCliente);
            int productoKey = _repo.GetProductoKeyByOrigen(v.IdProducto);
            int fuenteKey = _repo.GetFuenteKeyByOrigen(fuentes.FirstOrDefault()?.IdFuente ?? 1);
            int tiempoKey = dimTiempo.IdTiempoKey;

            if (clienteKey > 0 && productoKey > 0)
            {
                var factVenta = DimensionTransformer.CrearFactVenta(v, clienteKey, productoKey, fuenteKey, tiempoKey);
                _repo.InsertFactVentas(factVenta);
            }
        }
        _logger.LogInformation("Dim_Tiempo y Fact_Ventas cargadas exitosamente en VentasDW.");
    }
}