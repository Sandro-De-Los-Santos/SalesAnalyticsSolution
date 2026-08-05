using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ETL.Core.Data;

public class StagingWriter
{
    private readonly string _stagingPath;
    private readonly ILogger _logger;

    public StagingWriter(string stagingPath, ILogger logger)
    {
        _stagingPath = stagingPath;
        _logger = logger;
        Directory.CreateDirectory(_stagingPath);
    }

    public async Task GuardarAsync<T>(string nombreLote, List<T> datos, CancellationToken cancellationToken = default)
    {
        string archivo = Path.Combine(_stagingPath, $"{nombreLote}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        string json = JsonSerializer.Serialize(datos, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(archivo, json, cancellationToken);
        _logger.LogInformation("Staging: {n} registros de '{lote}' guardados en {archivo}", datos.Count, nombreLote, archivo);
    }
}