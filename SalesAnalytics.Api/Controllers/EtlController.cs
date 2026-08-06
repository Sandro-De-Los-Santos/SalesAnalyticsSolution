using ETL.Core;
using ETL.Core.Data;
using Microsoft.AspNetCore.Mvc;

namespace SalesAnalytics.Api.Controllers
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
                var connStr = _config.GetConnectionString("DefaultConnection") ?? "Server=(localdb)\\mssqllocaldb;Database=SalesAnalyticsDB;Trusted_Connection=True;TrustServerCertificate=True;";
                var csvPath = _config["StagingSettings:CsvBasePath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "..", "ETL.App", "CsvFiles");

                var runner = new EtlRunner(_loggerEtl, csvPath, connStr);
                runner.Ejecutar();

                return Ok(new
                {
                    mensaje = "Proceso ETL (Staging -> ODS -> DataWarehouse) ejecutado exitosamente.",
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
