namespace ETL.Core.Models
{
    public class DimProducto
    {
        public int ProductoKey { get; set; }
        public int IdProductoOrigen { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public bool Activo { get; set; }
        public string FuenteOrigen { get; set; } = string.Empty;
        public DateTime FechaCarga { get; set; }
    }
}
