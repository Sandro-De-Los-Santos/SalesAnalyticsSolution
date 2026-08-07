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
            (int?)ConsultarEscalar("SELECT IdTipoFuente FROM [AnalyticDB].[dbo].[TipoFuente] WHERE Nombre = @n", P("@n", nombre));

        public int? BuscarCategoriaPorNombre(string nombre) =>
            (int?)ConsultarEscalar("SELECT IdCategoria FROM [AnalyticDB].[dbo].[Categorias] WHERE Nombre = @n", P("@n", nombre));

        public string GetNombreCategoria(int idCategoria) =>
            (string?)ConsultarEscalar("SELECT Nombre FROM [AnalyticDB].[dbo].[Categorias] WHERE IdCategoria = @id", P("@id", idCategoria)) ?? "General";

        public string GetNombreTipoFuente(int idTipoFuente) =>
            (string?)ConsultarEscalar("SELECT Nombre FROM [AnalyticDB].[dbo].[TipoFuente] WHERE IdTipoFuente = @id", P("@id", idTipoFuente)) ?? "Desconocido";

        public bool ExisteCliente(int id) =>
            (int)ConsultarEscalar("SELECT COUNT(1) FROM [AnalyticDB].[dbo].[Clientes] WHERE IdCliente = @id", P("@id", id))! > 0;

        public bool ExisteEmailCliente(string email) =>
            (int)ConsultarEscalar("SELECT COUNT(1) FROM [AnalyticDB].[dbo].[Clientes] WHERE Email = @e", P("@e", email))! > 0;

        public bool ExisteProducto(int id) =>
            (int)ConsultarEscalar("SELECT COUNT(1) FROM [AnalyticDB].[dbo].[Productos] WHERE IdProducto = @id", P("@id", id))! > 0;

        public int InsertTipoFuente(TipoFuente t)
        {
            t.IdTipoFuente = GetNextId("[AnalyticDB].[dbo].[TipoFuente]", "IdTipoFuente");
            EjecutarProcedimiento("sp_InsertTipoFuente",
                P("@IdTipoFuente", t.IdTipoFuente), P("@Nombre", t.Nombre), P("@Descripcion", t.Descripcion));
            return t.IdTipoFuente;
        }

        public int InsertFuenteDatos(FuenteDatos f)
        {
            f.IdFuente = GetNextId("[AnalyticDB].[dbo].[FuenteDatos]", "IdFuente");
            EjecutarProcedimiento("sp_InsertFuenteDatos",
                P("@IdFuente", f.IdFuente), P("@NombreFuente", f.NombreFuente), P("@Descripcion", f.Descripcion),
                P("@FechaRegistro", f.FechaRegistro), P("@IdTipoFuente", f.IdTipoFuente));
            return f.IdFuente;
        }

        public int InsertCategoria(Categoria c)
        {
            c.IdCategoria = GetNextId("[AnalyticDB].[dbo].[Categorias]", "IdCategoria");
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
            v.IdVenta = GetNextId("[AnalyticDB].[dbo].[Ventas]", "IdVenta");
            EjecutarProcedimiento("sp_InsertVenta",
                P("@IdVenta", v.IdVenta), P("@IdCliente", v.IdCliente), P("@IdProducto", v.IdProducto),
                P("@Cantidad", v.Cantidad), P("@Precio", v.Precio), P("@Fecha", v.Fecha),
                P("@FuenteOrigen", v.FuenteOrigen), P("@FechaCarga", v.FechaCarga));
        }

        public int InsertLogInicio(int idFuente)
        {
            int id = GetNextId("[AnalyticDB].[dbo].[LogCargaETL]", "IdLog");
            EjecutarProcedimiento("sp_InsertLogInicio", P("@IdLog", id), P("@IdFuente", idFuente), P("@FechaInicio", DateTime.Now));
            return id;
        }

        public void ActualizarLogFin(int idLog, int procesados, int insertados, int rechazados, string estado, string? error = null) =>
            EjecutarProcedimiento("sp_ActualizarLogFin",
                P("@IdLog", idLog), P("@FechaFin", DateTime.Now), P("@Procesados", procesados),
                P("@Insertados", insertados), P("@Rechazados", rechazados), P("@Estado", estado), P("@MensajeError", error));

        // ==========================================
        // METODOS DATA WAREHOUSE (CARGA DE DIMENSIONES VentasDW)
        // ==========================================

        public int UpsertDimCliente(DimCliente c)
        {
            string sql = @"
                MERGE INTO [VentasDW].[dbo].[Dim_Cliente] AS Target
                USING (SELECT @IdClienteOrigen AS IdClienteOrigen) AS Source
                ON (Target.IdClienteOrigen = Source.IdClienteOrigen)
                WHEN MATCHED THEN
                    UPDATE SET Target.NombreCompleto = @NombreCompleto, Target.Email = @Email, Target.Ciudad = @Ciudad, Target.FechaCarga = @FechaCarga
                WHEN NOT MATCHED THEN
                    INSERT (IdClienteOrigen, NombreCompleto, Email, Ciudad, Pais, FechaCarga)
                    VALUES (@IdClienteOrigen, @NombreCompleto, @Email, @Ciudad, @Pais, @FechaCarga);";

            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@IdClienteOrigen", c.IdClienteOrigen);
            cmd.Parameters.AddWithValue("@NombreCompleto", c.NombreCompleto);
            cmd.Parameters.AddWithValue("@Email", c.Email ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Ciudad", c.Ciudad);
            cmd.Parameters.AddWithValue("@Pais", c.Pais);
            cmd.Parameters.AddWithValue("@FechaCarga", c.FechaCarga);
            cmd.ExecuteNonQuery();

            return GetClienteKeyByOrigen(c.IdClienteOrigen);
        }

        public int UpsertDimProducto(DimProducto p)
        {
            string sql = @"
                MERGE INTO [VentasDW].[dbo].[Dim_Producto] AS Target
                USING (SELECT @IdProductoOrigen AS IdProductoOrigen) AS Source
                ON (Target.IdProductoOrigen = Source.IdProductoOrigen)
                WHEN MATCHED THEN
                    UPDATE SET Target.NombreProducto = @NombreProducto, Target.Categoria = @Categoria, Target.PrecioActual = @PrecioActual, Target.FechaCarga = @FechaCarga
                WHEN NOT MATCHED THEN
                    INSERT (IdProductoOrigen, NombreProducto, Categoria, PrecioActual, FechaCarga)
                    VALUES (@IdProductoOrigen, @NombreProducto, @Categoria, @PrecioActual, @FechaCarga);";

            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@IdProductoOrigen", p.IdProductoOrigen);
            cmd.Parameters.AddWithValue("@NombreProducto", p.NombreProducto);
            cmd.Parameters.AddWithValue("@Categoria", p.Categoria);
            cmd.Parameters.AddWithValue("@PrecioActual", p.PrecioActual);
            cmd.Parameters.AddWithValue("@FechaCarga", p.FechaCarga);
            cmd.ExecuteNonQuery();

            return GetProductoKeyByOrigen(p.IdProductoOrigen);
        }

        public int UpsertDimFuenteDatos(DimFuenteDatos f)
        {
            string sql = @"
                MERGE INTO [VentasDW].[dbo].[Dim_Fuente] AS Target
                USING (SELECT @IdFuenteOrigen AS IdFuenteOrigen) AS Source
                ON (Target.IdFuenteOrigen = Source.IdFuenteOrigen)
                WHEN MATCHED THEN
                    UPDATE SET Target.NombreFuente = @NombreFuente, Target.TipoFuente = @TipoFuente, Target.FechaCarga = @FechaCarga
                WHEN NOT MATCHED THEN
                    INSERT (IdFuenteOrigen, NombreFuente, TipoFuente, FechaCarga)
                    VALUES (@IdFuenteOrigen, @NombreFuente, @TipoFuente, @FechaCarga);";

            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@IdFuenteOrigen", f.IdFuenteOrigen);
            cmd.Parameters.AddWithValue("@NombreFuente", f.NombreFuente);
            cmd.Parameters.AddWithValue("@TipoFuente", f.TipoFuente);
            cmd.Parameters.AddWithValue("@FechaCarga", f.FechaCarga);
            cmd.ExecuteNonQuery();

            return GetFuenteKeyByOrigen(f.IdFuenteOrigen);
        }

        public void UpsertDimTiempo(DimTiempo t)
        {
            string sql = @"
                IF NOT EXISTS (SELECT 1 FROM [VentasDW].[dbo].[Dim_Tiempo] WHERE IdTiempoKey = @IdTiempoKey)
                BEGIN
                    INSERT INTO [VentasDW].[dbo].[Dim_Tiempo] (IdTiempoKey, Fecha, Anio, Trimestre, Mes, NombreMes, Dia, DiaSemana)
                    VALUES (@IdTiempoKey, @Fecha, @Anio, @Trimestre, @Mes, @NombreMes, @Dia, @DiaSemana);
                END";

            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@IdTiempoKey", t.IdTiempoKey);
            cmd.Parameters.AddWithValue("@Fecha", t.Fecha);
            cmd.Parameters.AddWithValue("@Anio", t.Anio);
            cmd.Parameters.AddWithValue("@Trimestre", t.Trimestre);
            cmd.Parameters.AddWithValue("@Mes", t.Mes);
            cmd.Parameters.AddWithValue("@NombreMes", t.NombreMes);
            cmd.Parameters.AddWithValue("@Dia", t.Dia);
            cmd.Parameters.AddWithValue("@DiaSemana", t.DiaSemana);
            cmd.ExecuteNonQuery();
        }

        public void InsertFactVentas(FactVentas f)
        {
            string sql = @"
                IF NOT EXISTS (
                    SELECT 1 FROM [VentasDW].[dbo].[Fact_Ventas]
                    WHERE IdClienteKey = @ClienteKey AND IdProductoKey = @ProductoKey AND IdTiempoKey = @TiempoKey AND Cantidad = @Cantidad
                )
                BEGIN
                    INSERT INTO [VentasDW].[dbo].[Fact_Ventas] (IdClienteKey, IdProductoKey, IdFuenteKey, IdTiempoKey, Cantidad, PrecioUnitario, MontoTotal, FechaCarga)
                    VALUES (@ClienteKey, @ProductoKey, @FuenteKey, @TiempoKey, @Cantidad, @PrecioUnitario, @MontoTotal, @FechaCarga);
                END";

            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ClienteKey", f.ClienteKey);
            cmd.Parameters.AddWithValue("@ProductoKey", f.ProductoKey);
            cmd.Parameters.AddWithValue("@FuenteKey", f.FuenteKey);
            cmd.Parameters.AddWithValue("@TiempoKey", f.TiempoKey);
            cmd.Parameters.AddWithValue("@Cantidad", f.Cantidad);
            cmd.Parameters.AddWithValue("@PrecioUnitario", f.PrecioUnitario);
            cmd.Parameters.AddWithValue("@MontoTotal", f.MontoTotal);
            cmd.Parameters.AddWithValue("@FechaCarga", f.FechaCarga);
            cmd.ExecuteNonQuery();
        }

        public int GetClienteKeyByOrigen(int idOrigen)
        {
            object? res = ConsultarEscalar("SELECT IdClienteKey FROM [VentasDW].[dbo].[Dim_Cliente] WHERE IdClienteOrigen = @id", P("@id", idOrigen));
            return res != null ? Convert.ToInt32(res) : 0;
        }

        public int GetProductoKeyByOrigen(int idOrigen)
        {
            object? res = ConsultarEscalar("SELECT IdProductoKey FROM [VentasDW].[dbo].[Dim_Producto] WHERE IdProductoOrigen = @id", P("@id", idOrigen));
            return res != null ? Convert.ToInt32(res) : 0;
        }

        public int GetFuenteKeyByOrigen(int idFuenteOrigen)
        {
            object? res = ConsultarEscalar("SELECT IdFuenteKey FROM [VentasDW].[dbo].[Dim_Fuente] WHERE IdFuenteOrigen = @id", P("@id", idFuenteOrigen));
            return res != null ? Convert.ToInt32(res) : 0;
        }

        public List<FuenteDatos> ObtenerFuentesDatos()
        {
            var lista = new List<FuenteDatos>();
            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand("SELECT IdFuente, NombreFuente, Descripcion, FechaRegistro, IdTipoFuente FROM [AnalyticDB].[dbo].[FuenteDatos]", conn);
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
            using var cmd = new SqlCommand("SELECT IdCliente, Nombre, Email, Region, Activo, FuenteOrigen, FechaCarga FROM [AnalyticDB].[dbo].[Clientes]", conn);
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
                    FuenteOrigen = null,
                    FechaCarga = reader.GetDateTime(6)
                });
            }
            return lista;
        }

        public List<Producto> ObtenerProductos()
        {
            var lista = new List<Producto>();
            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand("SELECT IdProducto, Nombre, Precio, Activo, FuenteOrigen, FechaCarga, IdCategoria FROM [AnalyticDB].[dbo].[Productos]", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new Producto
                {
                    IdProducto = reader.GetInt32(0),
                    Nombre = reader.GetString(1),
                    Precio = reader.GetDecimal(2),
                    Activo = reader.GetBoolean(3),
                    FuenteOrigen = null,
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
            using var cmd = new SqlCommand("SELECT IdVenta, IdCliente, IdProducto, Cantidad, Precio, Fecha, FuenteOrigen, FechaCarga FROM [AnalyticDB].[dbo].[Ventas]", conn);
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
                    FuenteOrigen = null,
                    FechaCarga = reader.GetDateTime(7)
                });
            }
            return lista;
        }

        public Dictionary<string, int> ObtenerResumenDW()
        {
            var resumen = new Dictionary<string, int>();
            resumen["Dim_Cliente"] = Convert.ToInt32(ConsultarEscalar("SELECT COUNT(1) FROM [VentasDW].[dbo].[Dim_Cliente]") ?? 0);
            resumen["Dim_Producto"] = Convert.ToInt32(ConsultarEscalar("SELECT COUNT(1) FROM [VentasDW].[dbo].[Dim_Producto]") ?? 0);
            resumen["Dim_Fuente"] = Convert.ToInt32(ConsultarEscalar("SELECT COUNT(1) FROM [VentasDW].[dbo].[Dim_Fuente]") ?? 0);
            resumen["Dim_Tiempo"] = Convert.ToInt32(ConsultarEscalar("SELECT COUNT(1) FROM [VentasDW].[dbo].[Dim_Tiempo]") ?? 0);
            resumen["Fact_Ventas"] = Convert.ToInt32(ConsultarEscalar("SELECT COUNT(1) FROM [VentasDW].[dbo].[Fact_Ventas]") ?? 0);
            return resumen;
        }
    }
}