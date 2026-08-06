using ETL.Core.Data;
using Microsoft.AspNetCore.Mvc;

namespace SalesAnalytics.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DataWarehouseController : ControllerBase
    {
        private readonly IConfiguration _config;

        public DataWarehouseController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet("resumen")]
        public IActionResult ObtenerResumenDW()
        {
            try
            {
                var connStr = _config.GetConnectionString("DefaultConnection") ?? "Server=(localdb)\\mssqllocaldb;Database=SalesAnalyticsDB;Trusted_Connection=True;TrustServerCertificate=True;";
                var repo = new Repository(connStr);
                var resumen = repo.ObtenerResumenDW();
                return Ok(new
                {
                    baseDatos = "DataWarehouse - SalesAnalyticsDB",
                    fechaConsulta = DateTime.Now,
                    tablas = resumen
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
