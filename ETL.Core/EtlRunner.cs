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
        _logger.LogInformation("  [DW] Dim_Fuente  <- AnalyticDB.FuenteDatos : {n} registros a procesar", fuentes.Count);
        int cargadasFuente = 0;
        foreach (var f in fuentes)
        {
            string nombreTipo = _repo.GetNombreTipoFuente(f.IdTipoFuente);
            var dimFuente = DimensionTransformer.TransformarDimFuente(f, nombreTipo);
            _repo.UpsertDimFuenteDatos(dimFuente);
            cargadasFuente++;
        }
        _logger.LogInformation("  [DW] Dim_Fuente  -> {n} registros cargados en VentasDW", cargadasFuente);

        // 2. Carga Dim_Cliente
        var clientes = _repo.ObtenerClientes();
        _logger.LogInformation("  [DW] Dim_Cliente <- AnalyticDB.Clientes    : {n} registros a procesar", clientes.Count);
        int cargadasCliente = 0;
        foreach (var c in clientes)
        {
            var dimCliente = DimensionTransformer.TransformarDimCliente(c);
            _repo.UpsertDimCliente(dimCliente);
            cargadasCliente++;
        }
        _logger.LogInformation("  [DW] Dim_Cliente -> {n} registros cargados en VentasDW", cargadasCliente);

        // 3. Carga Dim_Producto
        var productos = _repo.ObtenerProductos();
        _logger.LogInformation("  [DW] Dim_Producto <- AnalyticDB.Productos  : {n} registros a procesar", productos.Count);
        int cargadasProducto = 0;
        foreach (var p in productos)
        {
            string nombreCat = _repo.GetNombreCategoria(p.IdCategoria.GetValueOrDefault());
            var dimProducto = DimensionTransformer.TransformarDimProducto(p, nombreCat);
            _repo.UpsertDimProducto(dimProducto);
            cargadasProducto++;
        }
        _logger.LogInformation("  [DW] Dim_Producto -> {n} registros cargados en VentasDW", cargadasProducto);

        // 4. Carga Dim_Tiempo y Fact_Ventas
        var ventas = _repo.ObtenerVentas();
        _logger.LogInformation("  [DW] Dim_Tiempo + Fact_Ventas <- AnalyticDB.Ventas : {n} registros a procesar", ventas.Count);
        int cargadasFact = 0;
        foreach (var v in ventas)
        {
            var dimTiempo = DimensionTransformer.TransformarDimTiempo(v.Fecha);
            _repo.UpsertDimTiempo(dimTiempo);

            int clienteKey  = _repo.GetClienteKeyByOrigen(v.IdCliente);
            int productoKey = _repo.GetProductoKeyByOrigen(v.IdProducto);
            int fuenteKey   = _repo.GetFuenteKeyByOrigen(fuentes.FirstOrDefault()?.IdFuente ?? 1);
            int tiempoKey   = dimTiempo.IdTiempoKey;

            if (clienteKey > 0 && productoKey > 0)
            {
                var factVenta = DimensionTransformer.CrearFactVenta(v, clienteKey, productoKey, fuenteKey, tiempoKey);
                _repo.InsertFactVentas(factVenta);
                cargadasFact++;
            }
        }
        _logger.LogInformation("  [DW] Dim_Tiempo  -> registros de fechas unicas cargados en VentasDW");
        _logger.LogInformation("  [DW] Fact_Ventas -> {n} registros cargados en VentasDW", cargadasFact);
    }
}