using ETL.Core.Extract;
using ETL.Core.Models;

namespace ETL.Core.Transform
{
    public static class VentaTransformer
    {
        public static List<Venta> TransformarTodas(
            List<OrderCsv> ordenes,
            List<OrderDetailCsv> detalles,
            HashSet<int> clientesValidos,
            HashSet<int> productosValidos,
            int idFuente,
            out int rechazadosPorCancelado,
            out int rechazadosPorReferencia)
        {
            var ventas = new List<Venta>();
            rechazadosPorCancelado = 0;
            rechazadosPorReferencia = 0;

            var ordenesPorId = ordenes.ToDictionary(o => o.OrderID);

            foreach (var detalle in detalles)
            {

                if (!ordenesPorId.TryGetValue(detalle.OrderID, out var orden))
                {
                    rechazadosPorReferencia++;
                    continue;
                }

                if (orden.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    rechazadosPorCancelado++;
                    continue;
                }

                if (!clientesValidos.Contains(orden.CustomerID) || !productosValidos.Contains(detalle.ProductID))
                {
                    rechazadosPorReferencia++;
                    continue;
                }

                if (detalle.Quantity <= 0 || detalle.TotalPrice <= 0)
                {
                    rechazadosPorReferencia++;
                    continue;
                }

                decimal precioUnitario = Math.Round(detalle.TotalPrice / detalle.Quantity, 2);

                ventas.Add(new Venta
                {
                    IdCliente = orden.CustomerID,
                    IdProducto = detalle.ProductID,
                    Cantidad = detalle.Quantity,
                    Precio = precioUnitario,
                    Fecha = orden.OrderDate,
                    FuenteOrigen = idFuente,
                    FechaCarga = DateTime.Now
                });
            }

            return ventas;
        }
    }
}