using CsvHelper;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace ETL.Core.Extract;

public class CsvExtractor<T> : IExtractor<T>
{
    private readonly string _filePath;
    private readonly ILogger _logger;

    public CsvExtractor(string filePath, ILogger logger)
    {
        _filePath = filePath;
        _logger = logger;
    }

    public async Task<List<T>> ExtractAsync(CancellationToken cancellationToken = default)
    {
        var resultado = new List<T>();
        _logger.LogInformation("Iniciando extracción CSV: {archivo}", _filePath);

        if (!File.Exists(_filePath))
        {
            _logger.LogWarning("Archivo no encontrado: {archivo}", _filePath);
            return resultado;
        }

        using var reader = new StreamReader(_filePath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        await foreach (var record in csv.GetRecordsAsync<T>(cancellationToken))
        {
            resultado.Add(record);
        }

        _logger.LogInformation("Extracción CSV completada: {archivo} -> {n} registros", _filePath, resultado.Count);
        return resultado;
    }
}