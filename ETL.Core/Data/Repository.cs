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

        private bool TieneColumna(string tabla, string columna)
        {
            try
            {
                string sql = @"
                    SELECT COUNT(1) 
                    FROM [VentasDW].INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = @t AND COLUMN_NAME = @c";
                return Convert.ToInt32(ConsultarEscalar(sql, P("@t", tabla), P("@c", columna)) ?? 0) > 0;
            }
            catch
            {
                return false;
            }
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

        

        public int UpsertDimCliente(DimCliente c)
        {
            bool tieneIdOrigen   = TieneColumna("Dim_Cliente", "IdClienteOrigen");
            bool tieneFechaCarga = TieneColumna("Dim_Cliente", "FechaCarga");
            string colOrigen = tieneIdOrigen ? "IdClienteOrigen" : "IdCliente";

            string updateFecha  = tieneFechaCarga ? ", Target.FechaCarga = @FechaCarga" : "";
            string insertCols   = tieneFechaCarga ? $"{colOrigen}, NombreCompleto, Email, Ciudad, Pais, FechaCarga" : $"{colOrigen}, NombreCompleto, Email, Ciudad, Pais";
            string insertVals   = tieneFechaCarga ? "@IdClienteOrigen, @NombreCompleto, @Email, @Ciudad, @Pais, @FechaCarga" : "@IdClienteOrigen, @NombreCompleto, @Email, @Ciudad, @Pais";

            string sql = $@"
                MERGE INTO [VentasDW].[dbo].[Dim_Cliente] AS Target
                USING (SELECT @IdClienteOrigen AS {colOrigen}) AS Source
                ON (Target.{colOrigen} = Source.{colOrigen})
                WHEN MATCHED THEN
                    UPDATE SET Target.NombreCompleto = @NombreCompleto, Target.Email = @Email, Target.Ciudad = @Ciudad{updateFecha}
                WHEN NOT MATCHED THEN
                    INSERT ({insertCols})
                    VALUES ({insertVals});";

            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@IdClienteOrigen", c.IdClienteOrigen);
            cmd.Parameters.AddWithValue("@NombreCompleto", c.NombreCompleto);
            cmd.Parameters.AddWithValue("@Email", c.Email ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Ciudad", c.Ciudad);
            cmd.Parameters.AddWithValue("@Pais", c.Pais);
            if (tieneFechaCarga) cmd.Parameters.AddWithValue("@FechaCarga", c.FechaCarga);
            cmd.ExecuteNonQuery();

            return GetClienteKeyByOrigen(c.IdClienteOrigen);
        }

        public int UpsertDimProducto(DimProducto p)
        {
            bool tieneIdOrigen   = TieneColumna("Dim_Producto", "IdProductoOrigen");
            bool tieneFechaCarga = TieneColumna("Dim_Producto", "FechaCarga");
            string colOrigen = tieneIdOrigen ? "IdProductoOrigen" : "IdProducto";

            string updateFecha = tieneFechaCarga ? ", Target.FechaCarga = @FechaCarga" : "";
            string insertCols  = tieneFechaCarga ? $"{colOrigen}, NombreProducto, Categoria, PrecioActual, FechaCarga" : $"{colOrigen}, NombreProducto, Categoria, PrecioActual";
            string insertVals  = tieneFechaCarga ? "@IdProductoOrigen, @NombreProducto, @Categoria, @PrecioActual, @FechaCarga" : "@IdProductoOrigen, @NombreProducto, @Categoria, @PrecioActual";

            string sql = $@"
                MERGE INTO [VentasDW].[dbo].[Dim_Producto] AS Target
                USING (SELECT @IdProductoOrigen AS {colOrigen}) AS Source
                ON (Target.{colOrigen} = Source.{colOrigen})
                WHEN MATCHED THEN
                    UPDATE SET Target.NombreProducto = @NombreProducto, Target.Categoria = @Categoria, Target.PrecioActual = @PrecioActual{updateFecha}
                WHEN NOT MATCHED THEN
                    INSERT ({insertCols})
                    VALUES ({insertVals});";

            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@IdProductoOrigen", p.IdProductoOrigen);
            cmd.Parameters.AddWithValue("@NombreProducto", p.NombreProducto);
            cmd.Parameters.AddWithValue("@Categoria", p.Categoria);
            cmd.Parameters.AddWithValue("@PrecioActual", p.PrecioActual);
            if (tieneFechaCarga) cmd.Parameters.AddWithValue("@FechaCarga", p.FechaCarga);
            cmd.ExecuteNonQuery();

            return GetProductoKeyByOrigen(p.IdProductoOrigen);
        }

        public int UpsertDimFuenteDatos(DimFuenteDatos f)
        {
            bool tieneIdOrigen   = TieneColumna("Dim_Fuente", "IdFuenteOrigen");
            bool tieneIdFuente   = TieneColumna("Dim_Fuente", "IdFuente");
            bool tieneFechaCarga = TieneColumna("Dim_Fuente", "FechaCarga");

            string updateFecha = tieneFechaCarga ? ", Target.FechaCarga = @FechaCarga" : "";

            string sql;
            if (tieneIdOrigen)
            {
                string insertCols = tieneFechaCarga ? "IdFuenteOrigen, NombreFuente, TipoFuente, FechaCarga" : "IdFuenteOrigen, NombreFuente, TipoFuente";
                string insertVals = tieneFechaCarga ? "@IdFuenteOrigen, @NombreFuente, @TipoFuente, @FechaCarga" : "@IdFuenteOrigen, @NombreFuente, @TipoFuente";
                sql = $@"
                    MERGE INTO [VentasDW].[dbo].[Dim_Fuente] AS Target
                    USING (SELECT @IdFuenteOrigen AS IdFuenteOrigen) AS Source
                    ON (Target.IdFuenteOrigen = Source.IdFuenteOrigen)
                    WHEN MATCHED THEN
                        UPDATE SET Target.NombreFuente = @NombreFuente, Target.TipoFuente = @TipoFuente{updateFecha}
                    WHEN NOT MATCHED THEN
                        INSERT ({insertCols})
                        VALUES ({insertVals});";
            }
            else if (tieneIdFuente)
            {
                string insertCols = tieneFechaCarga ? "IdFuente, NombreFuente, TipoFuente, FechaCarga" : "IdFuente, NombreFuente, TipoFuente";
                string insertVals = tieneFechaCarga ? "@IdFuenteOrigen, @NombreFuente, @TipoFuente, @FechaCarga" : "@IdFuenteOrigen, @NombreFuente, @TipoFuente";
                sql = $@"
                    MERGE INTO [VentasDW].[dbo].[Dim_Fuente] AS Target
                    USING (SELECT @IdFuenteOrigen AS IdFuente) AS Source
                    ON (Target.IdFuente = Source.IdFuente)
                    WHEN MATCHED THEN
                        UPDATE SET Target.NombreFuente = @NombreFuente, Target.TipoFuente = @TipoFuente{updateFecha}
                    WHEN NOT MATCHED THEN
                        INSERT ({insertCols})
                        VALUES ({insertVals});";
            }
            else
            {
                string insertCols = tieneFechaCarga ? "NombreFuente, TipoFuente, FechaCarga" : "NombreFuente, TipoFuente";
                string insertVals = tieneFechaCarga ? "@NombreFuente, @TipoFuente, @FechaCarga" : "@NombreFuente, @TipoFuente";
                sql = $@"
                    MERGE INTO [VentasDW].[dbo].[Dim_Fuente] AS Target
                    USING (SELECT @NombreFuente AS NombreFuente) AS Source
                    ON (Target.NombreFuente = Source.NombreFuente)
                    WHEN MATCHED THEN
                        UPDATE SET Target.TipoFuente = @TipoFuente{updateFecha}
                    WHEN NOT MATCHED THEN
                        INSERT ({insertCols})
                        VALUES ({insertVals});";
            }

            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@IdFuenteOrigen", f.IdFuenteOrigen);
            cmd.Parameters.AddWithValue("@NombreFuente", f.NombreFuente);
            cmd.Parameters.AddWithValue("@TipoFuente", f.TipoFuente);
            if (tieneFechaCarga) cmd.Parameters.AddWithValue("@FechaCarga", f.FechaCarga);
            cmd.ExecuteNonQuery();

            return GetFuenteKeyByOrigen(f.IdFuenteOrigen, f.NombreFuente);
        }

        public void UpsertDimTiempo(DimTiempo t)
        {
            bool tieneIdKey = TieneColumna("Dim_Tiempo", "IdTiempoKey");
            string colKey = tieneIdKey ? "IdTiempoKey" : "TiempoKey";

            string sql = $@"
                IF NOT EXISTS (SELECT 1 FROM [VentasDW].[dbo].[Dim_Tiempo] WHERE {colKey} = @IdTiempoKey)
                BEGIN
                    INSERT INTO [VentasDW].[dbo].[Dim_Tiempo] ({colKey}, Fecha, Anio, Trimestre, Mes, NombreMes, Dia, DiaSemana)
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
            bool tieneIdClienteKey = TieneColumna("Fact_Ventas", "IdClienteKey");
            bool tieneFechaCarga   = TieneColumna("Fact_Ventas", "FechaCarga");
            string colCliente  = tieneIdClienteKey ? "IdClienteKey" : "ClienteKey";
            string colProducto = TieneColumna("Fact_Ventas", "IdProductoKey") ? "IdProductoKey" : "ProductoKey";
            string colFuente   = TieneColumna("Fact_Ventas", "IdFuenteKey")   ? "IdFuenteKey"   : "FuenteKey";
            string colTiempo   = TieneColumna("Fact_Ventas", "IdTiempoKey")   ? "IdTiempoKey"   : "TiempoKey";

            
            string insertCols = tieneFechaCarga
                ? $"IdOrdenOrigen, {colCliente}, {colProducto}, {colFuente}, {colTiempo}, Cantidad, PrecioUnitario, FechaCarga"
                : $"IdOrdenOrigen, {colCliente}, {colProducto}, {colFuente}, {colTiempo}, Cantidad, PrecioUnitario";
            string insertVals = tieneFechaCarga
                ? "@IdOrdenOrigen, @ClienteKey, @ProductoKey, @FuenteKey, @TiempoKey, @Cantidad, @PrecioUnitario, @FechaCarga"
                : "@IdOrdenOrigen, @ClienteKey, @ProductoKey, @FuenteKey, @TiempoKey, @Cantidad, @PrecioUnitario";

            string sql = $@"
                IF NOT EXISTS (
                    SELECT 1 FROM [VentasDW].[dbo].[Fact_Ventas]
                    WHERE IdOrdenOrigen = @IdOrdenOrigen
                )
                BEGIN
                    INSERT INTO [VentasDW].[dbo].[Fact_Ventas] ({insertCols})
                    VALUES ({insertVals});
                END";

            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@IdOrdenOrigen", f.IdOrdenOrigen);
            cmd.Parameters.AddWithValue("@ClienteKey", f.ClienteKey);
            cmd.Parameters.AddWithValue("@ProductoKey", f.ProductoKey);
            cmd.Parameters.AddWithValue("@FuenteKey", f.FuenteKey);
            cmd.Parameters.AddWithValue("@TiempoKey", f.TiempoKey);
            cmd.Parameters.AddWithValue("@Cantidad", f.Cantidad);
            cmd.Parameters.AddWithValue("@PrecioUnitario", f.PrecioUnitario);
            if (tieneFechaCarga) cmd.Parameters.AddWithValue("@FechaCarga", f.FechaCarga);
            cmd.ExecuteNonQuery();
        }

        public int GetClienteKeyByOrigen(int idOrigen)
        {
            bool tieneIdKey = TieneColumna("Dim_Cliente", "IdClienteKey");
            string colKey = tieneIdKey ? "IdClienteKey" : "ClienteKey";
            bool tieneIdOrigen = TieneColumna("Dim_Cliente", "IdClienteOrigen");
            string colOrigen = tieneIdOrigen ? "IdClienteOrigen" : "IdCliente";

            object? res = ConsultarEscalar($"SELECT TOP 1 {colKey} FROM [VentasDW].[dbo].[Dim_Cliente] WHERE {colOrigen} = @id", P("@id", idOrigen));
            return res != null ? Convert.ToInt32(res) : 0;
        }

        public int GetProductoKeyByOrigen(int idOrigen)
        {
            bool tieneIdKey = TieneColumna("Dim_Producto", "IdProductoKey");
            string colKey = tieneIdKey ? "IdProductoKey" : "ProductoKey";
            bool tieneIdOrigen = TieneColumna("Dim_Producto", "IdProductoOrigen");
            string colOrigen = tieneIdOrigen ? "IdProductoOrigen" : "IdProducto";

            object? res = ConsultarEscalar($"SELECT TOP 1 {colKey} FROM [VentasDW].[dbo].[Dim_Producto] WHERE {colOrigen} = @id", P("@id", idOrigen));
            return res != null ? Convert.ToInt32(res) : 0;
        }

        public int GetFuenteKeyByOrigen(int idFuenteOrigen, string nombreFuente = "")
        {
            bool tieneIdFuenteKey = TieneColumna("Dim_Fuente", "IdFuenteKey");
            string colKey = tieneIdFuenteKey ? "IdFuenteKey" : "FuenteKey";

            bool tieneIdOrigen = TieneColumna("Dim_Fuente", "IdFuenteOrigen");
            bool tieneIdFuente = TieneColumna("Dim_Fuente", "IdFuente");

            string sql;
            if (tieneIdOrigen)
                sql = $"SELECT TOP 1 {colKey} FROM [VentasDW].[dbo].[Dim_Fuente] WHERE IdFuenteOrigen = @id";
            else if (tieneIdFuente)
                sql = $"SELECT TOP 1 {colKey} FROM [VentasDW].[dbo].[Dim_Fuente] WHERE IdFuente = @id";
            else
                sql = $"SELECT TOP 1 {colKey} FROM [VentasDW].[dbo].[Dim_Fuente] WHERE NombreFuente = @n";

            object? res = ConsultarEscalar(sql, P("@id", idFuenteOrigen), P("@n", nombreFuente));
            return res != null ? Convert.ToInt32(res) : 1;
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
            resumen["Dim_Cliente"]  = Convert.ToInt32(ConsultarEscalar("SELECT COUNT(1) FROM [VentasDW].[dbo].[Dim_Cliente]") ?? 0);
            resumen["Dim_Producto"] = Convert.ToInt32(ConsultarEscalar("SELECT COUNT(1) FROM [VentasDW].[dbo].[Dim_Producto]") ?? 0);
            resumen["Dim_Fuente"]   = Convert.ToInt32(ConsultarEscalar("SELECT COUNT(1) FROM [VentasDW].[dbo].[Dim_Fuente]") ?? 0);
            resumen["Dim_Tiempo"]   = Convert.ToInt32(ConsultarEscalar("SELECT COUNT(1) FROM [VentasDW].[dbo].[Dim_Tiempo]") ?? 0);
            resumen["Fact_Ventas"]  = Convert.ToInt32(ConsultarEscalar("SELECT COUNT(1) FROM [VentasDW].[dbo].[Fact_Ventas]") ?? 0);
            return resumen;
        }

        public int ContarClientesAnalytic()    => Convert.ToInt32(ConsultarEscalar("SELECT COUNT(1) FROM [AnalyticDB].[dbo].[Clientes]") ?? 0);
        public int ContarProductosAnalytic()   => Convert.ToInt32(ConsultarEscalar("SELECT COUNT(1) FROM [AnalyticDB].[dbo].[Productos]") ?? 0);
        public int ContarCategoriasAnalytic()  => Convert.ToInt32(ConsultarEscalar("SELECT COUNT(1) FROM [AnalyticDB].[dbo].[Categorias]") ?? 0);
        public int ContarVentasAnalytic()      => Convert.ToInt32(ConsultarEscalar("SELECT COUNT(1) FROM [AnalyticDB].[dbo].[Ventas]") ?? 0);
        public int ContarFuentesAnalytic()     => Convert.ToInt32(ConsultarEscalar("SELECT COUNT(1) FROM [AnalyticDB].[dbo].[FuenteDatos]") ?? 0);
    }
}