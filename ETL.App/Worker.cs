using System.Diagnostics;
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
        _logger.LogInformation("=== Iniciando proceso de EXTRACCIÓN ETL ===");
        var cronometro = Stopwatch.StartNew();
        string basePath = _config["CsvSettings:BasePath"] ?? "CsvFiles";

        var csvClientes = new CsvExtractor<CustomerCsv>(
            Path.Combine(basePath, _config["CsvSettings:Clientes"]!),
            _loggerFactory.CreateLogger("CsvExtractor<Cliente>"));

        var csvProductos = new CsvExtractor<ProductCsv>(
            Path.Combine(basePath, _config["CsvSettings:Productos"]!),
            _loggerFactory.CreateLogger("CsvExtractor<Producto>"));

        var csvOrdenes = new CsvExtractor<OrderCsv>(
            Path.Combine(basePath, _config["CsvSettings:Ordenes"]!),
            _loggerFactory.CreateLogger("CsvExtractor<Orden>"));

        var csvDetalles = new CsvExtractor<OrderDetailCsv>(
            Path.Combine(basePath, _config["CsvSettings:DetalleOrdenes"]!),
            _loggerFactory.CreateLogger("CsvExtractor<DetalleOrden>"));

        var apiClientes = new ApiExtractor<ClienteApiRaw>(
            _httpClientFactory.CreateClient("ExternalApi"),
            _config["ApiSettings:ClientesEndpoint"]!,
            _loggerFactory.CreateLogger("ApiExtractor<Cliente>"));

        var dbVentas = new DatabaseExtractor(
            _config.GetConnectionString("VentasLegacyDB")!,
            _config["ExternalDbSettings:HistoricalSalesQuery"]!,
            _loggerFactory.CreateLogger("DatabaseExtractor"));

        var tClientesCsv = csvClientes.ExtractAsync(stoppingToken);
        var tProductosCsv = csvProductos.ExtractAsync(stoppingToken);
        var tOrdenesCsv = csvOrdenes.ExtractAsync(stoppingToken);
        var tDetallesCsv = csvDetalles.ExtractAsync(stoppingToken);
        var tApiClientes = apiClientes.ExtractAsync(stoppingToken);
        var tDbVentas = dbVentas.ExtractAsync(stoppingToken);

        await Task.WhenAll(tClientesCsv, tProductosCsv, tOrdenesCsv, tDetallesCsv, tApiClientes, tDbVentas);

        await _stagingWriter.GuardarAsync("clientes_csv", tClientesCsv.Result, stoppingToken);
        await _stagingWriter.GuardarAsync("productos_csv", tProductosCsv.Result, stoppingToken);
        await _stagingWriter.GuardarAsync("ordenes_csv", tOrdenesCsv.Result, stoppingToken);
        await _stagingWriter.GuardarAsync("detalle_ordenes_csv", tDetallesCsv.Result, stoppingToken);
        await _stagingWriter.GuardarAsync("clientes_api", tApiClientes.Result, stoppingToken);
        await _stagingWriter.GuardarAsync("ventas_historicas_db", tDbVentas.Result, stoppingToken);

        cronometro.Stop();
        _logger.LogInformation(
            "=== Extracción completada en {ms} ms | CSV: {a}+{b}+{c}+{d} | API: {e} | BD: {f} registros ===",
            cronometro.ElapsedMilliseconds,
            tClientesCsv.Result.Count, tProductosCsv.Result.Count,
            tOrdenesCsv.Result.Count, tDetallesCsv.Result.Count,
            tApiClientes.Result.Count, tDbVentas.Result.Count);

        _lifetime.StopApplication();
    }
}