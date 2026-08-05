using ETL.Core.Models;
using Microsoft.Extensions.Logging;

namespace ETL.Core.Data;

public class DataLoader
{
    private readonly Repository _repo;
    private readonly ILogger _logger;

    public DataLoader(Repository repo, ILogger logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public int CargarClientes(List<Cliente> clientes)
    {
        int insertados = 0;
        foreach (var c in clientes)
        {
            if (_repo.ExisteCliente(c.IdCliente) ||
                (c.Email != null && _repo.ExisteEmailCliente(c.Email)))
                continue;

            _repo.InsertCliente(c);
            insertados++;
        }
        _logger.LogInformation("DataLoader: {n} clientes cargados", insertados);
        return insertados;
    }

    public int CargarProductos(List<Producto> productos)
    {
        int insertados = 0;
        foreach (var p in productos)
        {
            if (_repo.ExisteProducto(p.IdProducto))
                continue;

            _repo.InsertProducto(p);
            insertados++;
        }
        _logger.LogInformation("DataLoader: {n} productos cargados", insertados);
        return insertados;
    }

    public int CargarVentas(List<Venta> ventas)
    {
        int insertados = 0;
        foreach (var v in ventas)
        {
            _repo.InsertVenta(v);
            insertados++;
        }
        _logger.LogInformation("DataLoader: {n} ventas cargadas", insertados);
        return insertados;
    }
}