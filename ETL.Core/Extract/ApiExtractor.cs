using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace ETL.Core.Extract;

public class ApiExtractor<T> : IExtractor<T>
{
    private readonly HttpClient _http;
    private readonly string _endpoint;
    private readonly ILogger _logger;

    public ApiExtractor(HttpClient http, string endpoint, ILogger logger)
    {
        _http = http;
        _endpoint = endpoint;
        _logger = logger;
    }

    public async Task<List<T>> ExtractAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Iniciando extracción API: {endpoint}", _endpoint);
        try
        {
            var datos = await _http.GetFromJsonAsync<List<T>>(_endpoint, cancellationToken);
            var resultado = datos ?? new List<T>();
            _logger.LogInformation("Extracción API completada: {endpoint} -> {n} registros", _endpoint, resultado.Count);
            return resultado;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al consumir API {endpoint}", _endpoint);
            return new List<T>();
        }
    }
}