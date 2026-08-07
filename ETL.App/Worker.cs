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
        var cronometroTotal = Stopwatch.StartNew();

        _logger.LogInformation("════════════════════════════════════════════════════════");
        _logger.LogInformation("   SISTEMA ETL - SalesAnalyticsSolution");
        _logger.LogInformation("   Inicio del proceso: {time}", DateTimeOffset.Now);
        _logger.LogInformation("════════════════════════════════════════════════════════");

        try
        {
            // ==================================================================
            // FASE 1: EXTRACCION DESDE ARCHIVOS CSV
            // ==================================================================
            _logger.LogInformation("");
            _logger.LogInformation("────────────────────────────────────────────────────────");
            _logger.LogInformation("  [FASE 1] EXTRACCION DESDE ARCHIVOS CSV");
            _logger.LogInformation("────────────────────────────────────────────────────────");

            string basePath = _config["CsvSettings:BasePath"] ?? "CsvFiles";
            var sw1 = Stopwatch.StartNew();

            var csvClientes = new CsvExtractor<CustomerCsv>(
                Path.Combine(basePath, _config["CsvSettings:Clientes"] ?? "customers.csv"),
                _loggerFactory.CreateLogger("CSV-Clientes"));

            var csvProductos = new CsvExtractor<ProductCsv>(
                Path.Combine(basePath, _config["CsvSettings:Productos"] ?? "products.csv"),
                _loggerFactory.CreateLogger("CSV-Productos"));

            var csvOrdenes = new CsvExtractor<OrderCsv>(
                Path.Combine(basePath, _config["CsvSettings:Ordenes"] ?? "orders.csv"),
                _loggerFactory.CreateLogger("CSV-Ordenes"));

            var csvDetalles = new CsvExtractor<OrderDetailCsv>(
                Path.Combine(basePath, _config["CsvSettings:DetalleOrdenes"] ?? "order_details.csv"),
                _loggerFactory.CreateLogger("CSV-DetalleOrdenes"));

            var tClientesCsv  = csvClientes.ExtractAsync(stoppingToken);
            var tProductosCsv = csvProductos.ExtractAsync(stoppingToken);
            var tOrdenesCsv   = csvOrdenes.ExtractAsync(stoppingToken);
            var tDetallesCsv  = csvDetalles.ExtractAsync(stoppingToken);

            await Task.WhenAll(tClientesCsv, tProductosCsv, tOrdenesCsv, tDetallesCsv);
            sw1.Stop();

            _logger.LogInformation("  >> customers.csv     → {n} registros extraídos", tClientesCsv.Result.Count);
            _logger.LogInformation("  >> products.csv      → {n} registros extraídos", tProductosCsv.Result.Count);
            _logger.LogInformation("  >> orders.csv        → {n} registros extraídos", tOrdenesCsv.Result.Count);
            _logger.LogInformation("  >> order_details.csv → {n} registros extraídos", tDetallesCsv.Result.Count);
            _logger.LogInformation("  [FASE 1] COMPLETADA en {ms} ms", sw1.ElapsedMilliseconds);

            await _stagingWriter.GuardarAsync("clientes_csv",      tClientesCsv.Result,  stoppingToken);
            await _stagingWriter.GuardarAsync("productos_csv",     tProductosCsv.Result, stoppingToken);
            await _stagingWriter.GuardarAsync("ordenes_csv",       tOrdenesCsv.Result,   stoppingToken);
            await _stagingWriter.GuardarAsync("detalle_ordenes_csv", tDetallesCsv.Result, stoppingToken);

            // ==================================================================
            // FASE 2: EXTRACCION DESDE API EXTERNA (modo demostración)
            // ==================================================================
            _logger.LogInformation("");
            _logger.LogInformation("────────────────────────────────────────────────────────");
            _logger.LogInformation("  [FASE 2] EXTRACCION DESDE API EXTERNA");
            _logger.LogInformation("────────────────────────────────────────────────────────");

            var sw2 = Stopwatch.StartNew();
            var apiClientes = new ApiExtractor<ClienteApiRaw>(
                _httpClientFactory.CreateClient("ExternalApi"),
                _config["ApiSettings:ClientesEndpoint"] ?? "users",
                _loggerFactory.CreateLogger("API-Clientes"));

            var resultadoApi = await apiClientes.ExtractAsync(stoppingToken);
            sw2.Stop();

            _logger.LogInformation("  >> API ({endpoint})  → {n} registros extraídos",
                _config["ApiSettings:ClientesEndpoint"] ?? "users", resultadoApi.Count);

            if (resultadoApi.Count > 0)
            {
                _logger.LogInformation("  [INFO] Se encontraron datos en la API. En una versión futura se integrarían a AnalyticDB.");
                await _stagingWriter.GuardarAsync("clientes_api", resultadoApi, stoppingToken);
            }
            else
            {
                _logger.LogInformation("  [INFO] La API externa no retornó datos en este ciclo. No se carga nada del API.");
            }

            _logger.LogInformation("  [FASE 2] COMPLETADA en {ms} ms", sw2.ElapsedMilliseconds);

            // ==================================================================
            // FASE 3: EXTRACCION DESDE BASE DE DATOS RELACIONAL (AnalyticDB)
            //         y CARGA DE DIMENSIONES EN EL DATA WAREHOUSE (VentasDW)
            // ==================================================================
            _logger.LogInformation("");
            _logger.LogInformation("────────────────────────────────────────────────────────");
            _logger.LogInformation("  [FASE 3] EXTRACCION DESDE AnalyticDB → CARGA EN VentasDW");
            _logger.LogInformation("────────────────────────────────────────────────────────");

            string connStr = _config.GetConnectionString("AnalyticDB")
                ?? throw new InvalidOperationException("Cadena de conexión 'AnalyticDB' no encontrada en appsettings.json.");

            var sw3 = Stopwatch.StartNew();
            var runnerLogger = _loggerFactory.CreateLogger<EtlRunner>();
            var etlRunner = new EtlRunner(runnerLogger, basePath, connStr);

            etlRunner.CargarSoloDataWarehouse();
            sw3.Stop();

            _logger.LogInformation("  [FASE 3] COMPLETADA en {ms} ms", sw3.ElapsedMilliseconds);

            // ==================================================================
            // RESUMEN FINAL
            // ==================================================================
            cronometroTotal.Stop();
            _logger.LogInformation("");
            _logger.LogInformation("════════════════════════════════════════════════════════");
            _logger.LogInformation("  RESUMEN FINAL DEL PROCESO ETL");
            _logger.LogInformation("  Fase 1 - CSV       : {c} clientes, {p} productos, {o} órdenes, {d} detalles",
                tClientesCsv.Result.Count, tProductosCsv.Result.Count,
                tOrdenesCsv.Result.Count, tDetallesCsv.Result.Count);
            _logger.LogInformation("  Fase 2 - API       : {a} registros extraídos (no cargados al DW)", resultadoApi.Count);
            _logger.LogInformation("  Fase 3 - AnalyticDB: Dimensiones cargadas en VentasDW ✔");
            _logger.LogInformation("  Tiempo total       : {ms} ms", cronometroTotal.ElapsedMilliseconds);
            _logger.LogInformation("════════════════════════════════════════════════════════");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante la ejecución del proceso ETL");
        }

        _lifetime.StopApplication();
    }
}