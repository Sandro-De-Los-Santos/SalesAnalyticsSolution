using System.Globalization;
using ETL.Core.Models;

namespace ETL.Core.Transform
{
    public static class DimensionTransformer
    {
        public static DimCliente TransformarDimCliente(Cliente c)
        {
            return new DimCliente
            {
                IdClienteOrigen = c.IdCliente,
                NombreCompleto = c.Nombre.Trim(),
                Email = (c.Email ?? string.Empty).ToLowerInvariant().Trim(),
                Ciudad = string.IsNullOrWhiteSpace(c.Region) ? "Desconocida" : c.Region.Trim(),
                Pais = "Desconocido",
                FechaCarga = DateTime.Now
            };
        }

        public static DimProducto TransformarDimProducto(Producto p, string nombreCategoria)
        {
            return new DimProducto
            {
                IdProductoOrigen = p.IdProducto,
                NombreProducto = p.Nombre.Trim(),
                Categoria = string.IsNullOrWhiteSpace(nombreCategoria) ? "General" : nombreCategoria.Trim(),
                PrecioActual = p.Precio,
                FechaCarga = DateTime.Now
            };
        }

        public static DimFuenteDatos TransformarDimFuente(FuenteDatos f, string nombreTipoFuente)
        {
            return new DimFuenteDatos
            {
                IdFuenteOrigen = f.IdFuente,
                NombreFuente = f.NombreFuente,
                Descripcion = f.Descripcion ?? string.Empty,
                TipoFuente = string.IsNullOrWhiteSpace(nombreTipoFuente) ? "Desconocido" : nombreTipoFuente,
                FechaRegistro = f.FechaRegistro,
                FechaCarga = DateTime.Now
            };
        }

        public static DimTiempo TransformarDimTiempo(DateTime fecha)
        {
            var cultureEs = new CultureInfo("es-ES");
            int tiempoKey = int.Parse(fecha.ToString("yyyyMMdd"));

            return new DimTiempo
            {
                IdTiempoKey = tiempoKey,
                Fecha = fecha.Date,
                Anio = fecha.Year,
                Trimestre = ((fecha.Month - 1) / 3) + 1,
                Mes = fecha.Month,
                NombreMes = cultureEs.DateTimeFormat.GetMonthName(fecha.Month),
                Dia = fecha.Day,
                DiaSemana = cultureEs.DateTimeFormat.GetDayName(fecha.DayOfWeek)
            };
        }

        public static FactVentas CrearFactVenta(Venta v, int clienteKey, int productoKey, int fuenteKey, int tiempoKey)
        {
            return new FactVentas
            {
                IdOrdenOrigen = v.IdVenta,      
                ClienteKey = clienteKey,
                ProductoKey = productoKey,
                FuenteKey = fuenteKey,
                TiempoKey = tiempoKey,
                Cantidad = v.Cantidad,
                PrecioUnitario = v.Precio,
                FechaCarga = DateTime.Now
            };
        }
    }
}
