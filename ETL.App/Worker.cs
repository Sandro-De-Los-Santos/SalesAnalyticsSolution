using System.Diagnostics;
using ETL.Core;
using ETL.Core.Data;
using ETL.Core.Extract;

namespace ETL.App;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly StagingWriter _stagingWriter;
    private readonly IHostApplicationLifetime _lifetime;

    public Worker(
        ILogger<Worker> logger,
        ILoggerFactory loggerFactory,
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        StagingWriter stagingWriter,
        IHostApplicationLifetime lifetime)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _config = config;
        _httpClientFactory = httpClientFactory;
        _stagingWriter = stagingWriter;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string connStr = _config.GetConnectionString("AnalyticDB")
            ?? throw new InvalidOperationException("Cadena de conexión 'AnalyticDB' no encontrada en appsettings.json.");
        string basePath = _config["CsvSettings:BasePath"] ?? "CsvFiles";

        var repo      = new Repository(connStr);
        var runnerLog = _loggerFactory.CreateLogger<EtlRunner>();

        // =================================================================
        // PROCESO 1: EXTRACCION DE DATOS DESDE TODAS LAS FUENTES
        // =================================================================
        _logger.LogInformation("=== PROCESO 1: EXTRACCION DE DATOS ===");
        var sw1 = Stopwatch.StartNew();

        // --- Fuente 1: Archivos CSV ---
        _logger.LogInformation("Iniciando extraccion CSV: {path}", basePath);

        var csvClientes  = new CsvExtractor<CustomerCsv>(Path.Combine(basePath, _config["CsvSettings:Clientes"]  ?? "customers.csv"),     _loggerFactory.CreateLogger("CsvExtractor<Cliente>"));
        var csvProductos = new CsvExtractor<ProductCsv>( Path.Combine(basePath, _config["CsvSettings:Productos"] ?? "products.csv"),      _loggerFactory.CreateLogger("CsvExtractor<Producto>"));
        var csvOrdenes   = new CsvExtractor<OrderCsv>(   Path.Combine(basePath, _config["CsvSettings:Ordenes"]   ?? "orders.csv"),        _loggerFactory.CreateLogger("CsvExtractor<Orden>"));
        var csvDetalles  = new CsvExtractor<OrderDetailCsv>(Path.Combine(basePath, _config["CsvSettings:DetalleOrdenes"] ?? "order_details.csv"), _loggerFactory.CreateLogger("CsvExtractor<DetalleOrden>"));

        var tCli = csvClientes.ExtractAsync(stoppingToken);
        var tPro = csvProductos.ExtractAsync(stoppingToken);
        var tOrd = csvOrdenes.ExtractAsync(stoppingToken);
        var tDet = csvDetalles.ExtractAsync(stoppingToken);
        await Task.WhenAll(tCli, tPro, tOrd, tDet);

        await _stagingWriter.GuardarAsync("clientes_csv",       tCli.Result, stoppingToken);
        await _stagingWriter.GuardarAsync("productos_csv",      tPro.Result, stoppingToken);
        await _stagingWriter.GuardarAsync("ordenes_csv",        tOrd.Result, stoppingToken);
        await _stagingWriter.GuardarAsync("detalle_ordenes_csv",tDet.Result, stoppingToken);

        int totalCsv = tCli.Result.Count + tPro.Result.Count + tOrd.Result.Count + tDet.Result.Count;

        // --- Fuente 2: API Externa ---
        _logger.LogInformation("Iniciando extraccion API: {endpoint}", _config["ApiSettings:ClientesEndpoint"]);

        var apiClientes   = new ApiExtractor<ClienteApiRaw>(
            _httpClientFactory.CreateClient("ExternalApi"),
            _config["ApiSettings:ClientesEndpoint"] ?? "users",
            _loggerFactory.CreateLogger("ApiExtractor<Cliente>"));

        var resultadoApi = await apiClientes.ExtractAsync(stoppingToken);

        if (resultadoApi.Count > 0)
            await _stagingWriter.GuardarAsync("clientes_api", resultadoApi, stoppingToken);

        // --- Fuente 3: Base de Datos Relacional AnalyticDB ---
        _logger.LogInformation("Iniciando extraccion BD: AnalyticDB");

        int nClientes   = repo.ContarClientesAnalytic();
        int nProductos  = repo.ContarProductosAnalytic();
        int nCategorias = repo.ContarCategoriasAnalytic();
        int nVentas     = repo.ContarVentasAnalytic();
        int nFuentes    = repo.ContarFuentesAnalytic();

        _logger.LogInformation("Extraccion BD completada: AnalyticDB.Clientes    -> {n} registros", nClientes);
        _logger.LogInformation("Extraccion BD completada: AnalyticDB.Productos   -> {n} registros", nProductos);
        _logger.LogInformation("Extraccion BD completada: AnalyticDB.Categorias  -> {n} registros", nCategorias);
        _logger.LogInformation("Extraccion BD completada: AnalyticDB.Ventas      -> {n} registros", nVentas);
        _logger.LogInformation("Extraccion BD completada: AnalyticDB.FuenteDatos -> {n} registros", nFuentes);

        int totalBd = nClientes + nProductos + nCategorias + nVentas + nFuentes;

        sw1.Stop();
        _logger.LogInformation("=== Extraccion completada en {ms} ms | CSV: {csv} | API: {api} | BD: {bd} registros ===",
            sw1.ElapsedMilliseconds, totalCsv, resultadoApi.Count, totalBd);

        // =================================================================
        // PROCESO 2: CARGA DE DIMENSIONES AnalyticDB → VentasDW
        // =================================================================
        _logger.LogInformation("=== PROCESO 2: CARGA DE DIMENSIONES → VentasDW ===");
        var sw2 = Stopwatch.StartNew();

        var etlRunner = new EtlRunner(runnerLog, basePath, connStr);
        etlRunner.CargarSoloDataWarehouse();

        var resumenDW = repo.ObtenerResumenDW();
        sw2.Stop();

        _logger.LogInformation("=== Carga de dimensiones completada en {ms} ms | {r} ===",
            sw2.ElapsedMilliseconds,
            string.Join(" | ", resumenDW.Select(kv => $"{kv.Key}: {kv.Value}")));

        _lifetime.StopApplication();
    }
}