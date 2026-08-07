namespace ETL.Core.Models
{
    public class DimCliente
    {
        public int IdClienteKey { get; set; }
        public int IdClienteOrigen { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Ciudad { get; set; } = string.Empty;
        public string Pais { get; set; } = "Desconocido";
        public DateTime FechaCarga { get; set; }
    }
}
