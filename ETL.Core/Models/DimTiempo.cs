namespace ETL.Core.Models
{
    public class DimTiempo
    {
        public int IdTiempoKey { get; set; }
        public DateTime Fecha { get; set; }
        public int Anio { get; set; }
        public int Trimestre { get; set; }
        public int Mes { get; set; }
        public string NombreMes { get; set; } = string.Empty;
        public int Dia { get; set; }
        public string DiaSemana { get; set; } = string.Empty;
    }
}
