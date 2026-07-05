using System.Data;
using Microsoft.Data.SqlClient;
using ETL.Core.Models;

namespace ETL.Core.Data
{
    public class Repository
    {
        private readonly DatabaseConnection _db = new();

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
    }
}