namespace ETL.Core.Models
{
    public class Cliente
    {
        public int IdCliente { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Region { get; set; }
        public bool Activo { get; set; }
        public int? FuenteOrigen { get; set; }
        public DateTime FechaCarga { get; set; }
    }
}