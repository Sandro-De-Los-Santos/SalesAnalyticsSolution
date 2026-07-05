namespace ETL.Core.Models
{
    public class FuenteDatos
    {
        public int IdFuente { get; set; }
        public string NombreFuente { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public DateTime FechaRegistro { get; set; }
        public int IdTipoFuente { get; set; }
    }
}