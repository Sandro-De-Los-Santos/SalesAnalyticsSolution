-- ============================================================================
-- SCRIPT DE POBLADO DE DIMENSIONES Y TABLA DE HECHOS PARA DATA WAREHOUSE (VentasDW)
-- Lee de la base de datos relacional existente: [AnalyticDB]
-- Y carga las tablas de dimensiones en: [VentasDW]
-- ============================================================================

USE [VentasDW];
GO

-- 1. POBLAR DIMENSION CLIENTE (Dim_Cliente)
PRINT 'Poblando Dim_Cliente desde AnalyticDB.dbo.Clientes...';

MERGE INTO [VentasDW].[dbo].[Dim_Cliente] AS Target
USING (
    SELECT 
        IdCliente AS IdClienteOrigen,
        Nombre AS NombreCompleto,
        Email,
        ISNULL(Region, 'Desconocida') AS Ciudad,
        'Desconocido' AS Pais,
        GETDATE() AS FechaCarga
    FROM [AnalyticDB].[dbo].[Clientes]
) AS Source
ON (Target.IdClienteOrigen = Source.IdClienteOrigen)
WHEN MATCHED THEN
    UPDATE SET 
        Target.NombreCompleto = Source.NombreCompleto,
        Target.Email = Source.Email,
        Target.Ciudad = Source.Ciudad,
        Target.FechaCarga = Source.FechaCarga
WHEN NOT MATCHED THEN
    INSERT (IdClienteOrigen, NombreCompleto, Email, Ciudad, Pais, FechaCarga)
    VALUES (Source.IdClienteOrigen, Source.NombreCompleto, Source.Email, Source.Ciudad, Source.Pais, Source.FechaCarga);

GO

-- 2. POBLAR DIMENSION PRODUCTO (Dim_Producto)
PRINT 'Poblando Dim_Producto desde AnalyticDB.dbo.Productos...';

MERGE INTO [VentasDW].[dbo].[Dim_Producto] AS Target
USING (
    SELECT 
        p.IdProducto AS IdProductoOrigen,
        p.Nombre AS NombreProducto,
        ISNULL(c.Nombre, 'General') AS Categoria,
        p.Precio AS PrecioActual,
        GETDATE() AS FechaCarga
    FROM [AnalyticDB].[dbo].[Productos] p
    LEFT JOIN [AnalyticDB].[dbo].[Categorias] c ON p.IdCategoria = c.IdCategoria
) AS Source
ON (Target.IdProductoOrigen = Source.IdProductoOrigen)
WHEN MATCHED THEN
    UPDATE SET 
        Target.NombreProducto = Source.NombreProducto,
        Target.Categoria = Source.Categoria,
        Target.PrecioActual = Source.PrecioActual,
        Target.FechaCarga = Source.FechaCarga
WHEN NOT MATCHED THEN
    INSERT (IdProductoOrigen, NombreProducto, Categoria, PrecioActual, FechaCarga)
    VALUES (Source.IdProductoOrigen, Source.NombreProducto, Source.Categoria, Source.PrecioActual, Source.FechaCarga);

GO

-- 3. POBLAR DIMENSION FUENTE (Dim_Fuente)
PRINT 'Poblando Dim_Fuente desde AnalyticDB.dbo.FuenteDatos...';

MERGE INTO [VentasDW].[dbo].[Dim_Fuente] AS Target
USING (
    SELECT 
        f.IdFuente AS IdFuenteOrigen,
        f.NombreFuente,
        ISNULL(tf.Nombre, 'CSV') AS TipoFuente,
        GETDATE() AS FechaCarga
    FROM [AnalyticDB].[dbo].[FuenteDatos] f
    LEFT JOIN [AnalyticDB].[dbo].[TipoFuente] tf ON f.IdTipoFuente = tf.IdTipoFuente
) AS Source
ON (Target.IdFuenteOrigen = Source.IdFuenteOrigen)
WHEN MATCHED THEN
    UPDATE SET 
        Target.NombreFuente = Source.NombreFuente,
        Target.TipoFuente = Source.TipoFuente,
        Target.FechaCarga = Source.FechaCarga
WHEN NOT MATCHED THEN
    INSERT (IdFuenteOrigen, NombreFuente, TipoFuente, FechaCarga)
    VALUES (Source.IdFuenteOrigen, Source.NombreFuente, Source.TipoFuente, Source.FechaCarga);

GO

-- 4. POBLAR DIMENSION TIEMPO (Dim_Tiempo)
PRINT 'Poblando Dim_Tiempo desde fechas de AnalyticDB.dbo.Ventas...';

INSERT INTO [VentasDW].[dbo].[Dim_Tiempo] (IdTiempoKey, Fecha, Anio, Trimestre, Mes, NombreMes, Dia, DiaSemana)
SELECT DISTINCT
    CAST(CONVERT(VARCHAR(8), v.Fecha, 112) AS INT) AS IdTiempoKey,
    CAST(v.Fecha AS DATE) AS Fecha,
    YEAR(v.Fecha) AS Anio,
    DATEPART(QUARTER, v.Fecha) AS Trimestre,
    MONTH(v.Fecha) AS Mes,
    DATENAME(MONTH, v.Fecha) AS NombreMes,
    DAY(v.Fecha) AS Dia,
    DATENAME(WEEKDAY, v.Fecha) AS DiaSemana
FROM [AnalyticDB].[dbo].[Ventas] v
WHERE NOT EXISTS (
    SELECT 1 FROM [VentasDW].[dbo].[Dim_Tiempo] dt 
    WHERE dt.IdTiempoKey = CAST(CONVERT(VARCHAR(8), v.Fecha, 112) AS INT)
);

GO

-- 5. POBLAR TABLA DE HECHOS (Fact_Ventas)
PRINT 'Poblando Fact_Ventas relacionando claves sustitutas (Surrogate Keys)...';

INSERT INTO [VentasDW].[dbo].[Fact_Ventas] (IdClienteKey, IdProductoKey, IdFuenteKey, IdTiempoKey, Cantidad, PrecioUnitario, MontoTotal, FechaCarga)
SELECT 
    dc.IdClienteKey,
    dp.IdProductoKey,
    ISNULL(df.IdFuenteKey, 1) AS IdFuenteKey,
    CAST(CONVERT(VARCHAR(8), v.Fecha, 112) AS INT) AS IdTiempoKey,
    v.Cantidad,
    v.Precio AS PrecioUnitario,
    (v.Cantidad * v.Precio) AS MontoTotal,
    GETDATE() AS FechaCarga
FROM [AnalyticDB].[dbo].[Ventas] v
JOIN [VentasDW].[dbo].[Dim_Cliente] dc ON v.IdCliente = dc.IdClienteOrigen
JOIN [VentasDW].[dbo].[Dim_Producto] dp ON v.IdProducto = dp.IdProductoOrigen
LEFT JOIN [VentasDW].[dbo].[Dim_Fuente] df ON v.FuenteOrigen = CAST(df.IdFuenteOrigen AS VARCHAR)
WHERE NOT EXISTS (
    SELECT 1 FROM [VentasDW].[dbo].[Fact_Ventas] fv
    WHERE fv.IdClienteKey = dc.IdClienteKey 
      AND fv.IdProductoKey = dp.IdProductoKey 
      AND fv.IdTiempoKey = CAST(CONVERT(VARCHAR(8), v.Fecha, 112) AS INT)
      AND fv.Cantidad = v.Cantidad
);

GO

PRINT '=== Carga de Dimensiones y Hechos en VentasDW completada con éxito ===';
