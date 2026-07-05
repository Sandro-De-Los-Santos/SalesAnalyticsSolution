using ETL.Core.Extract;
using ETL.Core.Models;

namespace ETL.Core.Transform
{
    public static class ClienteTransformer
    {
        public static Cliente? Transformar(CustomerCsv c, int idFuente)
        {

            if (c.CustomerID <= 0 || string.IsNullOrWhiteSpace(c.FirstName))
                return null; 

            string nombreCompleto = $"{c.FirstName} {c.LastName}".Trim();
            string? region = null;
            if (!string.IsNullOrWhiteSpace(c.City) || !string.IsNullOrWhiteSpace(c.Country))
                region = $"{c.City}, {c.Country}".Trim(',', ' ');

            return new Cliente
            {
                IdCliente = c.CustomerID,
                Nombre = nombreCompleto,
                Email = string.IsNullOrWhiteSpace(c.Email) ? null : c.Email.Trim(),
                Region = region,
                Activo = true,
                FuenteOrigen = idFuente,
                FechaCarga = DateTime.Now
            };
        }
    }
}