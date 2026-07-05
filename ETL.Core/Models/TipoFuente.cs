namespace ETL.Core.Models
{
    public class TipoFuente
    {
        public int IdTipoFuente { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
    }
}