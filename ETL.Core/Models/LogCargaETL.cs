namespace ETL.Core.Models
{
    public class LogCargaETL
    {
        public int IdLog { get; set; }
        public int? IdFuente { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public int? RegistrosProcesados { get; set; }
        public int? RegistrosInsertados { get; set; }
        public int? RegistrosRechazados { get; set; }
        public string Estado { get; set; } = "INICIADO";
        public string? MensajeError { get; set; }
    }
}