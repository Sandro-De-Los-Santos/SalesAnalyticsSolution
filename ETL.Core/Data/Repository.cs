using System.Data;
using Microsoft.Data.SqlClient;
using ETL.Core.Models;

namespace ETL.Core.Data
{
    public class Repository
    {
        private readonly DatabaseConnection _db;

        public Repository(string connectionString)
        {
            _db = new DatabaseConnection(connectionString);
        }

        private void EjecutarProcedimiento(string nombreProc, params SqlParameter[] parametros)
        {
            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(nombreProc, conn) { CommandType = CommandType.StoredProcedure };
            if (parametros.Length > 0) cmd.Parameters.AddRange(parametros);
            cmd.ExecuteNonQuery();
        }

        private object? ConsultarEscalar(string sql, params SqlParameter[] parametros)
        {
            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(sql, conn);
            if (parametros.Length > 0) cmd.Parameters.AddRange(parametros);
            return cmd.ExecuteScalar();
        }

        public int GetNextId(string tabla, string columnaId)
        {
            string sql = $"SELECT ISNULL(MAX({columnaId}), 0) + 1 FROM {tabla}";
            return (int)ConsultarEscalar(sql)!;
        }

        private static SqlParameter P(string nombre, object? valor) =>
            new(nombre, valor ?? DBNull.Value);

        public int? BuscarTipoFuentePorNombre(string nombre) =>
            (int?)ConsultarEscalar("SELECT IdTipoFuente FROM TipoFuente WHERE Nombre = @n", P("@n", nombre));

        public int? BuscarCategoriaPorNombre(string nombre) =>
            (int?)ConsultarEscalar("SELECT IdCategoria FROM Categorias WHERE Nombre = @n", P("@n", nombre));

        public string GetNombreCategoria(int idCategoria) =>
            (string?)ConsultarEscalar("SELECT Nombre FROM Categorias WHERE IdCategoria = @id", P("@id", idCategoria)) ?? "General";

        public string GetNombreTipoFuente(int idTipoFuente) =>
            (string?)ConsultarEscalar("SELECT Nombre FROM TipoFuente WHERE IdTipoFuente = @id", P("@id", idTipoFuente)) ?? "Desconocido";

        public bool ExisteCliente(int id) =>
            (int)ConsultarEscalar("SELECT COUNT(1) FROM Clientes WHERE IdCliente = @id", P("@id", id))! > 0;

        public bool ExisteEmailCliente(string email) =>
            (int)ConsultarEscalar("SELECT COUNT(1) FROM Clientes WHERE Email = @e", P("@e", email))! > 0;

        public bool ExisteProducto(int id) =>
            (int)ConsultarEscalar("SELECT COUNT(1) FROM Productos WHERE IdProducto = @id", P("@id", id))! > 0;

        public int InsertTipoFuente(TipoFuente t)
        {
            t.IdTipoFuente = GetNextId("TipoFuente", "IdTipoFuente");
            EjecutarProcedimiento("sp_InsertTipoFuente",
                P("@IdTipoFuente", t.IdTipoFuente), P("@Nombre", t.Nombre), P("@Descripcion", t.Descripcion));
            return t.IdTipoFuente;
        }

        public int InsertFuenteDatos(FuenteDatos f)
        {
            f.IdFuente = GetNextId("FuenteDatos", "IdFuente");
            EjecutarProcedimiento("sp_InsertFuenteDatos",
                P("@IdFuente", f.IdFuente), P("@NombreFuente", f.NombreFuente), P("@Descripcion", f.Descripcion),
                P("@FechaRegistro", f.FechaRegistro), P("@IdTipoFuente", f.IdTipoFuente));
            return f.IdFuente;
        }

        public int InsertCategoria(Categoria c)
        {
            c.IdCategoria = GetNextId("Categorias", "IdCategoria");
            EjecutarProcedimiento("sp_InsertCategoria",
                P("@IdCategoria", c.IdCategoria), P("@Nombre", c.Nombre), P("@Descripcion", c.Descripcion));
            return c.IdCategoria;
        }

        public void InsertCliente(Cliente c) =>
            EjecutarProcedimiento("sp_InsertCliente",
                P("@IdCliente", c.IdCliente), P("@Nombre", c.Nombre), P("@Email", c.Email),
                P("@Region", c.Region), P("@Activo", c.Activo), P("@FuenteOrigen", c.FuenteOrigen),
                P("@FechaCarga", c.FechaCarga));

        public void InsertProducto(Producto p) =>
            EjecutarProcedimiento("sp_InsertProducto",
                P("@IdProducto", p.IdProducto), P("@Nombre", p.Nombre), P("@Precio", p.Precio),
                P("@Activo", p.Activo), P("@FuenteOrigen", p.FuenteOrigen), P("@FechaCarga", p.FechaCarga),
                P("@IdCategoria", p.IdCategoria));

        public void InsertVenta(Venta v)
        {
            v.IdVenta = GetNextId("Ventas", "IdVenta");
            EjecutarProcedimiento("sp_InsertVenta",
                P("@IdVenta", v.IdVenta), P("@IdCliente", v.IdCliente), P("@IdProducto", v.IdProducto),
                P("@Cantidad", v.Cantidad), P("@Precio", v.Precio), P("@Fecha", v.Fecha),
                P("@FuenteOrigen", v.FuenteOrigen), P("@FechaCarga", v.FechaCarga));
        }

        public int InsertLogInicio(int idFuente)
        {
            int id = GetNextId("LogCargaETL", "IdLog");
            EjecutarProcedimiento("sp_InsertLogInicio", P("@IdLog", id), P("@IdFuente", idFuente), P("@FechaInicio", DateTime.Now));
            return id;
        }

        public void ActualizarLogFin(int idLog, int procesados, int insertados, int rechazados, string estado, string? error = null) =>
            EjecutarProcedimiento("sp_ActualizarLogFin",
                P("@IdLog", idLog), P("@FechaFin", DateTime.Now), P("@Procesados", procesados),
                P("@Insertados", insertados), P("@Rechazados", rechazados), P("@Estado", estado), P("@MensajeError", error));

        // ==========================================
        // METODOS DATA WAREHOUSE (CARGA DE DIMENSIONES)
        // ==========================================

        public int UpsertDimCliente(DimCliente c)
        {
            EjecutarProcedimiento("sp_UpsertDimCliente",
                P("@IdClienteOrigen", c.IdClienteOrigen),
                P("@Nombre", c.Nombre),
                P("@Email", c.Email),
                P("@Region", c.Region),
                P("@Activo", c.Activo),
                P("@FuenteOrigen", c.FuenteOrigen),
                P("@FechaCarga", c.FechaCarga));

            return GetClienteKeyByOrigen(c.IdClienteOrigen);
        }

        public int UpsertDimProducto(DimProducto p)
        {
            EjecutarProcedimiento("sp_UpsertDimProducto",
                P("@IdProductoOrigen", p.IdProductoOrigen),
                P("@Nombre", p.Nombre),
                P("@Categoria", p.Categoria),
                P("@Precio", p.Precio),
                P("@Activo", p.Activo),
                P("@FuenteOrigen", p.FuenteOrigen),
                P("@FechaCarga", p.FechaCarga));

            return GetProductoKeyByOrigen(p.IdProductoOrigen);
        }

        public int UpsertDimFuenteDatos(DimFuenteDatos f)
        {
            EjecutarProcedimiento("sp_UpsertDimFuenteDatos",
                P("@IdFuenteOrigen", f.IdFuenteOrigen),
                P("@NombreFuente", f.NombreFuente),
                P("@Descripcion", f.Descripcion),
                P("@TipoFuente", f.TipoFuente),
                P("@FechaRegistro", f.FechaRegistro),
                P("@FechaCarga", f.FechaCarga));

            return GetFuenteKeyByOrigen(f.IdFuenteOrigen);
        }

        public void UpsertDimTiempo(DimTiempo t)
        {
            EjecutarProcedimiento("sp_UpsertDimTiempo",
                P("@TiempoKey", t.TiempoKey),
                P("@Fecha", t.Fecha),
                P("@Anio", t.Anio),
                P("@Trimestre", t.Trimestre),
                P("@Mes", t.Mes),
                P("@NombreMes", t.NombreMes),
                P("@Dia", t.Dia),
                P("@DiaSemana", t.DiaSemana),
                P("@EsFinDeSemana", t.EsFinDeSemana));
        }

        public void InsertFactVentas(FactVentas f)
        {
            EjecutarProcedimiento("sp_InsertFactVentas",
                P("@ClienteKey", f.ClienteKey),
                P("@ProductoKey", f.ProductoKey),
                P("@FuenteKey", f.FuenteKey),
                P("@TiempoKey", f.TiempoKey),
                P("@Cantidad", f.Cantidad),
                P("@PrecioUnitario", f.PrecioUnitario),
                P("@MontoTotal", f.MontoTotal),
                P("@FechaCarga", f.FechaCarga));
        }

        public int GetClienteKeyByOrigen(int idOrigen)
        {
            object? res = ConsultarEscalar("SELECT ClienteKey FROM DimCliente WHERE IdClienteOrigen = @id", P("@id", idOrigen));
            return res != null ? Convert.ToInt32(res) : 0;
        }

        public int GetProductoKeyByOrigen(int idOrigen)
        {
            object? res = ConsultarEscalar("SELECT ProductoKey FROM DimProducto WHERE IdProductoOrigen = @id", P("@id", idOrigen));
            return res != null ? Convert.ToInt32(res) : 0;
        }

        public int GetFuenteKeyByOrigen(int idFuenteOrigen)
        {
            object? res = ConsultarEscalar("SELECT FuenteKey FROM DimFuenteDatos WHERE IdFuenteOrigen = @id", P("@id", idFuenteOrigen));
            return res != null ? Convert.ToInt32(res) : 0;
        }

        public List<FuenteDatos> ObtenerFuentesDatos()
        {
            var lista = new List<FuenteDatos>();
            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand("SELECT IdFuente, NombreFuente, Descripcion, FechaRegistro, IdTipoFuente FROM FuenteDatos", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new FuenteDatos
                {
                    IdFuente = reader.GetInt32(0),
                    NombreFuente = reader.GetString(1),
                    Descripcion = reader.IsDBNull(2) ? null : reader.GetString(2),
                    FechaRegistro = reader.GetDateTime(3),
                    IdTipoFuente = reader.GetInt32(4)
                });
            }
            return lista;
        }

        public List<Cliente> ObtenerClientes()
        {
            var lista = new List<Cliente>();
            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand("SELECT IdCliente, Nombre, Email, Region, Activo, FuenteOrigen, FechaCarga FROM Clientes", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new Cliente
                {
                    IdCliente = reader.GetInt32(0),
                    Nombre = reader.GetString(1),
                    Email = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Region = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Activo = reader.GetBoolean(4),
                    FuenteOrigen = reader.IsDBNull(5) ? null : (int?)null, // FuenteOrigen guardado como string en ODS; se ignora al leer para DW
                    FechaCarga = reader.GetDateTime(6)
                });
            }
            return lista;
        }

        public List<Producto> ObtenerProductos()
        {
            var lista = new List<Producto>();
            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand("SELECT IdProducto, Nombre, Precio, Activo, FuenteOrigen, FechaCarga, IdCategoria FROM Productos", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new Producto
                {
                    IdProducto = reader.GetInt32(0),
                    Nombre = reader.GetString(1),
                    Precio = reader.GetDecimal(2),
                    Activo = reader.GetBoolean(3),
                    FuenteOrigen = null, // FuenteOrigen se re-mapea desde el nombre del archivo en DW
                    FechaCarga = reader.GetDateTime(5),
                    IdCategoria = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6)
                });
            }
            return lista;
        }

        public List<Venta> ObtenerVentas()
        {
            var lista = new List<Venta>();
            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand("SELECT IdVenta, IdCliente, IdProducto, Cantidad, Precio, Fecha, FuenteOrigen, FechaCarga FROM Ventas", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new Venta
                {
                    IdVenta = reader.GetInt32(0),
                    IdCliente = reader.GetInt32(1),
                    IdProducto = reader.GetInt32(2),
                    Cantidad = reader.GetInt32(3),
                    Precio = reader.GetDecimal(4),
                    Fecha = reader.GetDateTime(5),
                    FuenteOrigen = null, // Clave surrogate se resuelve por separado en la carga DW
                    FechaCarga = reader.GetDateTime(7)
                });
            }
            return lista;
        }

        public Dictionary<string, int> ObtenerResumenDW()
        {
            var resumen = new Dictionary<string, int>();
            resumen["DimCliente"] = Convert.ToInt32(ConsultarEscalar("SELECT COUNT(1) FROM DimCliente") ?? 0);
            resumen["DimProducto"] = Convert.ToInt32(ConsultarEscalar("SELECT COUNT(1) FROM DimProducto") ?? 0);
            resumen["DimFuenteDatos"] = Convert.ToInt32(ConsultarEscalar("SELECT COUNT(1) FROM DimFuenteDatos") ?? 0);
            resumen["DimTiempo"] = Convert.ToInt32(ConsultarEscalar("SELECT COUNT(1) FROM DimTiempo") ?? 0);
            resumen["FactVentas"] = Convert.ToInt32(ConsultarEscalar("SELECT COUNT(1) FROM FactVentas") ?? 0);
            resumen["LogCargaETL"] = Convert.ToInt32(ConsultarEscalar("SELECT COUNT(1) FROM LogCargaETL") ?? 0);
            return resumen;
        }
    }
}