namespace ETL.Core.Models
{
    public class DimFuenteDatos
    {
        public int FuenteKey { get; set; }
        public int IdFuenteOrigen { get; set; }
        public string NombreFuente { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string TipoFuente { get; set; } = string.Empty;
        public DateTime FechaRegistro { get; set; }
        public DateTime FechaCarga { get; set; }
    }
}
