using ETL.Core.Data;
using ETL.Core.Extract;
using ETL.Core.Models;
using ETL.Core.Transform;

namespace ETL.Core
{
    public class EtlRunner
    {
        private readonly Repository _repo = new();
        private readonly CsvReaderService _reader = new();

        public void Ejecutar()
        {
            Console.WriteLine("Iniciando proceso de carga de datos...\n");

            // ---------- 1. Preparar TipoFuente y FuenteDatos ----------
            int idTipoFuenteCsv = _repo.BuscarTipoFuentePorNombre("CSV")
                ?? _repo.InsertTipoFuente(new TipoFuente { Nombre = "CSV", Descripcion = "Archivos planos CSV" });

            int idFuenteClientes = _repo.InsertFuenteDatos(new FuenteDatos
            { NombreFuente = "customers.csv", Descripcion = "Clientes", FechaRegistro = DateTime.Now, IdTipoFuente = idTipoFuenteCsv });

            int idFuenteProductos = _repo.InsertFuenteDatos(new FuenteDatos
            { NombreFuente = "products.csv", Descripcion = "Productos", FechaRegistro = DateTime.Now, IdTipoFuente = idTipoFuenteCsv });

            int idFuenteVentas = _repo.InsertFuenteDatos(new FuenteDatos
            { NombreFuente = "orders.csv + order_details.csv", Descripcion = "Ventas", FechaRegistro = DateTime.Now, IdTipoFuente = idTipoFuenteCsv });

            int idLog = _repo.InsertLogInicio(idFuenteClientes);

            int totalProcesados = 0, totalInsertados = 0, totalRechazados = 0;

            try
            {
                // ---------- 2. Clientes ----------
                Console.WriteLine("Leyendo clientes...");
                var clientesCsv = _reader.Leer<CustomerCsv>("CsvFiles/customers.csv");
                var clientesValidos = new HashSet<int>();
                int clientesInsertados = 0, clientesRechazados = 0;

                foreach (var c in clientesCsv)
                {
                    totalProcesados++;
                    var cliente = ClienteTransformer.Transformar(c, idFuenteClientes);

                    if (cliente == null || _repo.ExisteCliente(cliente.IdCliente) ||
                        (cliente.Email != null && _repo.ExisteEmailCliente(cliente.Email)))
                    {
                        clientesRechazados++;
                        continue;
                    }

                    _repo.InsertCliente(cliente);
                    clientesValidos.Add(cliente.IdCliente);
                    clientesInsertados++;
                }
                totalInsertados += clientesInsertados;
                totalRechazados += clientesRechazados;
                Console.WriteLine($"Clientes -> leidos: {clientesCsv.Count}, insertados: {clientesInsertados}, rechazados: {clientesRechazados}\n");

                // ---------- 3. Productos ----------
                Console.WriteLine("Leyendo productos...");
                var productosCsv = _reader.Leer<ProductCsv>("CsvFiles/products.csv");
                var productoTransformer = new ProductoTransformer(_repo);
                var productosValidos = new HashSet<int>();
                int productosInsertados = 0, productosRechazados = 0;

                foreach (var p in productosCsv)
                {
                    totalProcesados++;
                    var producto = productoTransformer.Transformar(p, idFuenteProductos);

                    if (producto == null || _repo.ExisteProducto(producto.IdProducto))
                    {
                        productosRechazados++;
                        continue;
                    }

                    _repo.InsertProducto(producto);
                    productosValidos.Add(producto.IdProducto);
                    productosInsertados++;
                }
                totalInsertados += productosInsertados;
                totalRechazados += productosRechazados;
                Console.WriteLine($"Productos -> leidos: {productosCsv.Count}, insertados: {productosInsertados}, rechazados: {productosRechazados}\n");

                // ---------- 4. Ventas ----------
                Console.WriteLine("Leyendo ordenes y detalles de venta...");
                var ordenesCsv = _reader.Leer<OrderCsv>("CsvFiles/orders.csv");
                var detallesCsv = _reader.Leer<OrderDetailCsv>("CsvFiles/order_details.csv");

                var ventas = VentaTransformer.TransformarTodas(
                    ordenesCsv, detallesCsv, clientesValidos, productosValidos, idFuenteVentas,
                    out int rechazadosCancelado, out int rechazadosReferencia);

                int ventasInsertadas = 0;
                foreach (var venta in ventas)
                {
                    _repo.InsertVenta(venta);
                    ventasInsertadas++;
                }

                totalProcesados += detallesCsv.Count;
                totalInsertados += ventasInsertadas;
                totalRechazados += (rechazadosCancelado + rechazadosReferencia);

                Console.WriteLine($"Ventas -> leidas: {detallesCsv.Count}, insertadas: {ventasInsertadas}, rechazadas: {rechazadosCancelado + rechazadosReferencia}");
                Console.WriteLine($"(canceladas: {rechazadosCancelado}, sin referencia valida: {rechazadosReferencia})\n");

                // ---------- 5. Cerrar Log ----------
                _repo.ActualizarLogFin(idLog, totalProcesados, totalInsertados, totalRechazados, "COMPLETADO");

                // ---------- 6. Resumen final ----------
                Console.WriteLine("Proceso terminado.");
                Console.WriteLine($"Total procesados: {totalProcesados}");
                Console.WriteLine($"Total insertados: {totalInsertados}");
                Console.WriteLine($"Total rechazados: {totalRechazados}");
                Console.WriteLine("Estado: COMPLETADO");
            }
            catch (Exception ex)
            {
                _repo.ActualizarLogFin(idLog, totalProcesados, totalInsertados, totalRechazados, "ERROR", ex.Message);
                Console.WriteLine($"Ocurrio un error durante el proceso: {ex.Message}");
            }
        }
    }
}