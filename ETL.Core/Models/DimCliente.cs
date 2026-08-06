namespace ETL.Core.Models
{
    public class DimCliente
    {
        public int ClienteKey { get; set; }
        public int IdClienteOrigen { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public bool Activo { get; set; }
        public string FuenteOrigen { get; set; } = string.Empty;
        public DateTime FechaCarga { get; set; }
    }
}
