namespace ETL.Core.Models
{
    public class Producto
    {
        public int IdProducto { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public bool Activo { get; set; }
        public int? FuenteOrigen { get; set; }
        public DateTime FechaCarga { get; set; }
        public int? IdCategoria { get; set; }
    }
}