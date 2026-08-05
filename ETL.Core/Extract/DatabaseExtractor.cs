using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace ETL.Core.Extract;

public class VentaHistoricaRaw
{
    public string NumeroFactura { get; set; } = string.Empty;
    public int CodigoCliente { get; set; }
    public int CodigoProducto { get; set; }
    public DateTime FechaVenta { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
}

public class DatabaseExtractor : IExtractor<VentaHistoricaRaw>
{
    private readonly string _connectionString;
    private readonly string _query;
    private readonly ILogger _logger;

    public DatabaseExtractor(string connectionString, string query, ILogger logger)
    {
        _connectionString = connectionString;
        _query = query;
        _logger = logger;
    }

    public async Task<List<VentaHistoricaRaw>> ExtractAsync(CancellationToken cancellationToken = default)
    {
        var resultado = new List<VentaHistoricaRaw>();
        _logger.LogInformation("Iniciando extracción de BD externa (VentasLegacyDB)...");

        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            await using var cmd = new SqlCommand(_query, conn);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                resultado.Add(new VentaHistoricaRaw
                {
                    NumeroFactura = reader["NumeroFactura"].ToString() ?? string.Empty,
                    CodigoCliente = Convert.ToInt32(reader["CodigoCliente"]),
                    CodigoProducto = Convert.ToInt32(reader["CodigoProducto"]),
                    FechaVenta = Convert.ToDateTime(reader["FechaVenta"]),
                    Cantidad = Convert.ToInt32(reader["Cantidad"]),
                    PrecioUnitario = Convert.ToDecimal(reader["PrecioUnitario"])
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al extraer datos de VentasLegacyDB");
        }

        _logger.LogInformation("Extracción BD externa completada -> {n} registros", resultado.Count);
        return resultado;
    }
}