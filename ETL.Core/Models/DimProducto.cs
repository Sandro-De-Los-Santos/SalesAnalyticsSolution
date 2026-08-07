namespace ETL.Core.Models
{
    public class DimProducto
    {
        public int IdProductoKey { get; set; }
        public int IdProductoOrigen { get; set; }
        public string NombreProducto { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public decimal PrecioActual { get; set; }
        public DateTime FechaCarga { get; set; }
    }
}
