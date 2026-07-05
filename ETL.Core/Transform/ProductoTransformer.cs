using ETL.Core.Data;
using ETL.Core.Extract;
using ETL.Core.Models;

namespace ETL.Core.Transform
{
    public class ProductoTransformer
    {
        private readonly Repository _repo;
        private readonly Dictionary<string, int> _categoriasCache = new();

        public ProductoTransformer(Repository repo)
        {
            _repo = repo;
        }

        private int ObtenerOCrearCategoria(string nombreCategoria)
        {
            string clave = nombreCategoria.Trim();

            if (_categoriasCache.TryGetValue(clave, out int idCache))
                return idCache;

            int? idExistente = _repo.BuscarCategoriaPorNombre(clave);
            if (idExistente.HasValue)
            {
                _categoriasCache[clave] = idExistente.Value;
                return idExistente.Value;
            }

            var nuevaCategoria = new Categoria { Nombre = clave, Descripcion = $"Categoría generada automáticamente: {clave}" };
            int idNuevo = _repo.InsertCategoria(nuevaCategoria);
            _categoriasCache[clave] = idNuevo;
            return idNuevo;
        }

        public Producto? Transformar(ProductCsv p, int idFuente)
        {

            if (p.ProductID <= 0 || string.IsNullOrWhiteSpace(p.ProductName) || p.Price <= 0)
                return null;

            int idCategoria = ObtenerOCrearCategoria(
                string.IsNullOrWhiteSpace(p.Category) ? "Sin Categoria" : p.Category);

            return new Producto
            {
                IdProducto = p.ProductID,
                Nombre = p.ProductName.Trim(),
                Precio = p.Price,
                Activo = true,
                FuenteOrigen = idFuente,
                FechaCarga = DateTime.Now,
                IdCategoria = idCategoria
            };
        }
    }
}