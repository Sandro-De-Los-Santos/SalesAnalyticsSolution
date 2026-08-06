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
        ILoggerFactory _loggerFactory,
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        StagingWriter stagingWriter,
        IHostApplicationLifetime lifetime)
    {
        _logger = logger;
        this._loggerFactory = _loggerFactory;
        _config = config;
        _httpClientFactory = httpClientFactory;
        _stagingWriter = stagingWriter;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("=== [Worker Service] Iniciando Proceso Completo ETL y Carga Data Warehouse ===");
        var cronometro = Stopwatch.StartNew();
        string basePath = _config["CsvSettings:BasePath"] ?? "CsvFiles";

        try
        {
            var csvClientes = new CsvExtractor<CustomerCsv>(
                Path.Combine(basePath, _config["CsvSettings:Clientes"] ?? "customers.csv"),
                _loggerFactory.CreateLogger("CsvExtractor<Cliente>"));

            var csvProductos = new CsvExtractor<ProductCsv>(
                Path.Combine(basePath, _config["CsvSettings:Productos"] ?? "products.csv"),
                _loggerFactory.CreateLogger("CsvExtractor<Producto>"));

            var csvOrdenes = new CsvExtractor<OrderCsv>(
                Path.Combine(basePath, _config["CsvSettings:Ordenes"] ?? "orders.csv"),
                _loggerFactory.CreateLogger("CsvExtractor<Orden>"));

            var csvDetalles = new CsvExtractor<OrderDetailCsv>(
                Path.Combine(basePath, _config["CsvSettings:DetalleOrdenes"] ?? "order_details.csv"),
                _loggerFactory.CreateLogger("CsvExtractor<DetalleOrden>"));

            var tClientesCsv = csvClientes.ExtractAsync(stoppingToken);
            var tProductosCsv = csvProductos.ExtractAsync(stoppingToken);
            var tOrdenesCsv = csvOrdenes.ExtractAsync(stoppingToken);
            var tDetallesCsv = csvDetalles.ExtractAsync(stoppingToken);

            await Task.WhenAll(tClientesCsv, tProductosCsv, tOrdenesCsv, tDetallesCsv);

            await _stagingWriter.GuardarAsync("clientes_csv", tClientesCsv.Result, stoppingToken);
            await _stagingWriter.GuardarAsync("productos_csv", tProductosCsv.Result, stoppingToken);
            await _stagingWriter.GuardarAsync("ordenes_csv", tOrdenesCsv.Result, stoppingToken);
            await _stagingWriter.GuardarAsync("detalle_ordenes_csv", tDetallesCsv.Result, stoppingToken);

            _logger.LogInformation("Guardado en Staging finalizado. Ejecutando transformación y carga en DataWarehouse...");

            string connStr = _config.GetConnectionString("DefaultConnection") ?? "Server=(localdb)\\mssqllocaldb;Database=SalesAnalyticsDB;Trusted_Connection=True;TrustServerCertificate=True;";
            var runnerLogger = _loggerFactory.CreateLogger<EtlRunner>();
            var etlRunner = new EtlRunner(runnerLogger, basePath, connStr);

            etlRunner.Ejecutar();

            cronometro.Stop();
            _logger.LogInformation("=== [Worker Service] Proceso ETL finalizado exitosamente en {ms} ms ===", cronometro.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante la ejecución del Worker Service");
        }

        _lifetime.StopApplication();
    }
}