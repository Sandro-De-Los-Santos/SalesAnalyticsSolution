namespace ETL.Core.Models
{
    public class FactVentas
    {
        public int FactVentaKey { get; set; }
        public int IdOrdenOrigen { get; set; }
        public int ClienteKey { get; set; }
        public int ProductoKey { get; set; }
        public int FuenteKey { get; set; }
        public int TiempoKey { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public DateTime FechaCarga { get; set; }
    }
}
