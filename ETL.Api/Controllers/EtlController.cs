using ETL.Core;
using ETL.Core.Data;
using Microsoft.AspNetCore.Mvc;

namespace ETL.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EtlController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EtlRunner> _loggerEtl;

        public EtlController(IConfiguration config, ILogger<EtlRunner> loggerEtl)
        {
            _config = config;
            _loggerEtl = loggerEtl;
        }

        [HttpPost("ejecutar")]
        public IActionResult EjecutarEtl()
        {
            try
            {
                var connStr = _config.GetConnectionString("AnalyticDB") ?? "Server=Sandro;Database=AnalyticDB;Trusted_Connection=True;TrustServerCertificate=True;";
                var csvPath = _config["StagingSettings:CsvBasePath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "..", "ETL.App", "CsvFiles");

                var runner = new EtlRunner(_loggerEtl, csvPath, connStr);
                runner.CargarSoloDataWarehouse();

                return Ok(new
                {
                    mensaje = "Proceso de carga de Dimensiones al Data Warehouse (VentasDW) ejecutado exitosamente.",
                    fecha = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
