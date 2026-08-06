namespace ETL.Core.Models
{
    public class DimTiempo
    {
        public int TiempoKey { get; set; } // Formato YYYYMMDD (ej. 20260806)
        public DateTime Fecha { get; set; }
        public int Anio { get; set; }
        public int Trimestre { get; set; }
        public int Mes { get; set; }
        public string NombreMes { get; set; } = string.Empty;
        public int Dia { get; set; }
        public string DiaSemana { get; set; } = string.Empty;
        public bool EsFinDeSemana { get; set; }
    }
}
