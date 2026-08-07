-- ============================================================================
-- SCRIPT DE CREACION Y CARGA DE DIMENSIONES Y TABLA DE HECHOS PARA DATAWAREHOUSE
-- Proyecto: SalesAnalyticsSolution
-- ODS Database: AnalyticDB | DW Database: VentasDW (o SalesAnalyticsDB)
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'AnalyticDB')
BEGIN
    CREATE DATABASE [AnalyticDB];
END;
GO

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'VentasDW')
BEGIN
    CREATE DATABASE [VentasDW];
END;
GO

-- ----------------------------------------------------------------------------
-- 1. BASE DE DATOS RELACIONAL / ODS (AnalyticDB)
-- ----------------------------------------------------------------------------
USE [AnalyticDB];
GO

IF OBJECT_ID('Ventas', 'U') IS NOT NULL DROP TABLE Ventas;
IF OBJECT_ID('Productos', 'U') IS NOT NULL DROP TABLE Productos;
IF OBJECT_ID('Clientes', 'U') IS NOT NULL DROP TABLE Clientes;
IF OBJECT_ID('Categorias', 'U') IS NOT NULL DROP TABLE Categorias;
IF OBJECT_ID('LogCargaETL', 'U') IS NOT NULL DROP TABLE LogCargaETL;
IF OBJECT_ID('FuenteDatos', 'U') IS NOT NULL DROP TABLE FuenteDatos;
IF OBJECT_ID('TipoFuente', 'U') IS NOT NULL DROP TABLE TipoFuente;

CREATE TABLE TipoFuente (
    IdTipoFuente INT PRIMARY KEY,
    Nombre VARCHAR(100) NOT NULL,
    Descripcion VARCHAR(255) NULL
);

CREATE TABLE FuenteDatos (
    IdFuente INT PRIMARY KEY,
    NombreFuente VARCHAR(150) NOT NULL,
    Descripcion VARCHAR(255) NULL,
    FechaRegistro DATETIME NOT NULL,
    IdTipoFuente INT NOT NULL FOREIGN KEY REFERENCES TipoFuente(IdTipoFuente)
);

CREATE TABLE Categorias (
    IdCategoria INT PRIMARY KEY,
    Nombre VARCHAR(100) NOT NULL,
    Descripcion VARCHAR(255) NULL
);

CREATE TABLE Clientes (
    IdCliente INT PRIMARY KEY,
    Nombre VARCHAR(150) NOT NULL,
    Email VARCHAR(150) NULL,
    Region VARCHAR(100) NULL,
    Activo BIT NOT NULL,
    FuenteOrigen VARCHAR(150) NOT NULL,
    FechaCarga DATETIME NOT NULL
);

CREATE TABLE Productos (
    IdProducto INT PRIMARY KEY,
    Nombre VARCHAR(150) NOT NULL,
    Precio DECIMAL(18,2) NOT NULL,
    Activo BIT NOT NULL,
    FuenteOrigen VARCHAR(150) NOT NULL,
    FechaCarga DATETIME NOT NULL,
    IdCategoria INT NOT NULL FOREIGN KEY REFERENCES Categorias(IdCategoria)
);

CREATE TABLE Ventas (
    IdVenta INT PRIMARY KEY,
    IdCliente INT NOT NULL FOREIGN KEY REFERENCES Clientes(IdCliente),
    IdProducto INT NOT NULL FOREIGN KEY REFERENCES Productos(IdProducto),
    Cantidad INT NOT NULL,
    Precio DECIMAL(18,2) NOT NULL,
    Fecha DATETIME NOT NULL,
    FuenteOrigen VARCHAR(150) NOT NULL,
    FechaCarga DATETIME NOT NULL
);

CREATE TABLE LogCargaETL (
    IdLog INT PRIMARY KEY,
    IdFuente INT NOT NULL FOREIGN KEY REFERENCES FuenteDatos(IdFuente),
    FechaInicio DATETIME NOT NULL,
    FechaFin DATETIME NULL,
    Procesados INT DEFAULT 0,
    Insertados INT DEFAULT 0,
    Rechazados INT DEFAULT 0,
    Estado VARCHAR(50) NOT NULL,
    MensajeError VARCHAR(MAX) NULL
);
GO

-- STORED PROCEDURES PARA ODS (AnalyticDB)
CREATE OR ALTER PROCEDURE sp_InsertTipoFuente
    @IdTipoFuente INT, @Nombre VARCHAR(100), @Descripcion VARCHAR(255)
AS BEGIN
    INSERT INTO TipoFuente (IdTipoFuente, Nombre, Descripcion) VALUES (@IdTipoFuente, @Nombre, @Descripcion);
END;
GO

CREATE OR ALTER PROCEDURE sp_InsertFuenteDatos
    @IdFuente INT, @NombreFuente VARCHAR(150), @Descripcion VARCHAR(255), @FechaRegistro DATETIME, @IdTipoFuente INT
AS BEGIN
    INSERT INTO FuenteDatos (IdFuente, NombreFuente, Descripcion, FechaRegistro, IdTipoFuente)
    VALUES (@IdFuente, @NombreFuente, @Descripcion, @FechaRegistro, @IdTipoFuente);
END;
GO

CREATE OR ALTER PROCEDURE sp_InsertCategoria
    @IdCategoria INT, @Nombre VARCHAR(100), @Descripcion VARCHAR(255)
AS BEGIN
    INSERT INTO Categorias (IdCategoria, Nombre, Descripcion) VALUES (@IdCategoria, @Nombre, @Descripcion);
END;
GO

CREATE OR ALTER PROCEDURE sp_InsertCliente
    @IdCliente INT, @Nombre VARCHAR(150), @Email VARCHAR(150), @Region VARCHAR(100), @Activo BIT, @FuenteOrigen VARCHAR(150), @FechaCarga DATETIME
AS BEGIN
    INSERT INTO Clientes (IdCliente, Nombre, Email, Region, Activo, FuenteOrigen, FechaCarga)
    VALUES (@IdCliente, @Nombre, @Email, @Region, @Activo, @FuenteOrigen, @FechaCarga);
END;
GO

CREATE OR ALTER PROCEDURE sp_InsertProducto
    @IdProducto INT, @Nombre VARCHAR(150), @Precio DECIMAL(18,2), @Activo BIT, @FuenteOrigen VARCHAR(150), @FechaCarga DATETIME, @IdCategoria INT
AS BEGIN
    INSERT INTO Productos (IdProducto, Nombre, Precio, Activo, FuenteOrigen, FechaCarga, IdCategoria)
    VALUES (@IdProducto, @Nombre, @Precio, @Activo, @FuenteOrigen, @FechaCarga, @IdCategoria);
END;
GO

CREATE OR ALTER PROCEDURE sp_InsertVenta
    @IdVenta INT, @IdCliente INT, @IdProducto INT, @Cantidad INT, @Precio DECIMAL(18,2), @Fecha DATETIME, @FuenteOrigen VARCHAR(150), @FechaCarga DATETIME
AS BEGIN
    INSERT INTO Ventas (IdVenta, IdCliente, IdProducto, Cantidad, Precio, Fecha, FuenteOrigen, FechaCarga)
    VALUES (@IdVenta, @IdCliente, @IdProducto, @Cantidad, @Precio, @Fecha, @FuenteOrigen, @FechaCarga);
END;
GO

CREATE OR ALTER PROCEDURE sp_InsertLogInicio
    @IdLog INT, @IdFuente INT, @FechaInicio DATETIME
AS BEGIN
    INSERT INTO LogCargaETL (IdLog, IdFuente, FechaInicio, Estado) VALUES (@IdLog, @IdFuente, @FechaInicio, 'EN_PROCESO');
END;
GO

CREATE OR ALTER PROCEDURE sp_ActualizarLogFin
    @IdLog INT, @FechaFin DATETIME, @Procesados INT, @Insertados INT, @Rechazados INT, @Estado VARCHAR(50), @MensajeError VARCHAR(MAX) = NULL
AS BEGIN
    UPDATE LogCargaETL 
    SET FechaFin = @FechaFin, Procesados = @Procesados, Insertados = @Insertados, Rechazados = @Rechazados, Estado = @Estado, MensajeError = @MensajeError
    WHERE IdLog = @IdLog;
END;
GO


-- ----------------------------------------------------------------------------
-- 2. BASE DE DATOS DATA WAREHOUSE (VentasDW)
-- ----------------------------------------------------------------------------
USE [VentasDW];
GO

IF OBJECT_ID('FactVentas', 'U') IS NOT NULL DROP TABLE FactVentas;
IF OBJECT_ID('DimFuenteDatos', 'U') IS NOT NULL DROP TABLE DimFuenteDatos;
IF OBJECT_ID('DimCliente', 'U') IS NOT NULL DROP TABLE DimCliente;
IF OBJECT_ID('DimProducto', 'U') IS NOT NULL DROP TABLE DimProducto;
IF OBJECT_ID('DimTiempo', 'U') IS NOT NULL DROP TABLE DimTiempo;

CREATE TABLE DimFuenteDatos (
    FuenteKey INT IDENTITY(1,1) PRIMARY KEY,
    IdFuenteOrigen INT NOT NULL UNIQUE,
    NombreFuente VARCHAR(150) NOT NULL,
    Descripcion VARCHAR(255) NULL,
    TipoFuente VARCHAR(100) NOT NULL,
    FechaRegistro DATETIME NOT NULL,
    FechaCarga DATETIME NOT NULL DEFAULT GETDATE()
);

CREATE TABLE DimCliente (
    ClienteKey INT IDENTITY(1,1) PRIMARY KEY,
    IdClienteOrigen INT NOT NULL UNIQUE,
    Nombre VARCHAR(150) NOT NULL,
    Email VARCHAR(150) NULL,
    Region VARCHAR(100) NULL,
    Activo BIT NOT NULL,
    FuenteOrigen VARCHAR(150) NOT NULL,
    FechaCarga DATETIME NOT NULL DEFAULT GETDATE()
);

CREATE TABLE DimProducto (
    ProductoKey INT IDENTITY(1,1) PRIMARY KEY,
    IdProductoOrigen INT NOT NULL UNIQUE,
    Nombre VARCHAR(150) NOT NULL,
    Categoria VARCHAR(100) NOT NULL,
    Precio DECIMAL(18,2) NOT NULL,
    Activo BIT NOT NULL,
    FuenteOrigen VARCHAR(150) NOT NULL,
    FechaCarga DATETIME NOT NULL DEFAULT GETDATE()
);

CREATE TABLE DimTiempo (
    TiempoKey INT PRIMARY KEY, -- Formato YYYYMMDD (Ej: 20260806)
    Fecha DATE NOT NULL,
    Anio INT NOT NULL,
    Trimestre INT NOT NULL,
    Mes INT NOT NULL,
    NombreMes VARCHAR(20) NOT NULL,
    Dia INT NOT NULL,
    DiaSemana VARCHAR(20) NOT NULL,
    EsFinDeSemana BIT NOT NULL
);

CREATE TABLE FactVentas (
    FactVentaKey INT IDENTITY(1,1) PRIMARY KEY,
    ClienteKey INT NOT NULL FOREIGN KEY REFERENCES DimCliente(ClienteKey),
    ProductoKey INT NOT NULL FOREIGN KEY REFERENCES DimProducto(ProductoKey),
    FuenteKey INT NOT NULL FOREIGN KEY REFERENCES DimFuenteDatos(FuenteKey),
    TiempoKey INT NOT NULL FOREIGN KEY REFERENCES DimTiempo(TiempoKey),
    Cantidad INT NOT NULL,
    PrecioUnitario DECIMAL(18,2) NOT NULL,
    MontoTotal DECIMAL(18,2) NOT NULL,
    FechaCarga DATETIME NOT NULL DEFAULT GETDATE()
);
GO

-- PROCEDIMIENTOS ALMACENADOS PARA VENTASDW
CREATE OR ALTER PROCEDURE sp_UpsertDimFuenteDatos
    @IdFuenteOrigen INT, @NombreFuente VARCHAR(150), @Descripcion VARCHAR(255), @TipoFuente VARCHAR(100), @FechaRegistro DATETIME, @FechaCarga DATETIME
AS BEGIN
    MERGE INTO DimFuenteDatos AS Target
    USING (SELECT @IdFuenteOrigen AS IdFuenteOrigen) AS Source
    ON (Target.IdFuenteOrigen = Source.IdFuenteOrigen)
    WHEN MATCHED THEN
        UPDATE SET Target.NombreFuente = @NombreFuente, Target.Descripcion = @Descripcion, Target.TipoFuente = @TipoFuente, Target.FechaCarga = @FechaCarga
    WHEN NOT MATCHED THEN
        INSERT (IdFuenteOrigen, NombreFuente, Descripcion, TipoFuente, FechaRegistro, FechaCarga)
        VALUES (@IdFuenteOrigen, @NombreFuente, @Descripcion, @TipoFuente, @FechaRegistro, @FechaCarga);
END;
GO

CREATE OR ALTER PROCEDURE sp_UpsertDimCliente
    @IdClienteOrigen INT, @Nombre VARCHAR(150), @Email VARCHAR(150), @Region VARCHAR(100), @Activo BIT, @FuenteOrigen VARCHAR(150), @FechaCarga DATETIME
AS BEGIN
    MERGE INTO DimCliente AS Target
    USING (SELECT @IdClienteOrigen AS IdClienteOrigen) AS Source
    ON (Target.IdClienteOrigen = Source.IdClienteOrigen)
    WHEN MATCHED THEN
        UPDATE SET Target.Nombre = @Nombre, Target.Email = @Email, Target.Region = @Region, Target.Activo = @Activo, Target.FuenteOrigen = @FuenteOrigen, Target.FechaCarga = @FechaCarga
    WHEN NOT MATCHED THEN
        INSERT (IdClienteOrigen, Nombre, Email, Region, Activo, FuenteOrigen, FechaCarga)
        VALUES (@IdClienteOrigen, @Nombre, @Email, @Region, @Activo, @FuenteOrigen, @FechaCarga);
END;
GO

CREATE OR ALTER PROCEDURE sp_UpsertDimProducto
    @IdProductoOrigen INT, @Nombre VARCHAR(150), @Categoria VARCHAR(100), @Precio DECIMAL(18,2), @Activo BIT, @FuenteOrigen VARCHAR(150), @FechaCarga DATETIME
AS BEGIN
    MERGE INTO DimProducto AS Target
    USING (SELECT @IdProductoOrigen AS IdProductoOrigen) AS Source
    ON (Target.IdProductoOrigen = Source.IdProductoOrigen)
    WHEN MATCHED THEN
        UPDATE SET Target.Nombre = @Nombre, Target.Categoria = @Categoria, Target.Precio = @Precio, Target.Activo = @Activo, Target.FuenteOrigen = @FuenteOrigen, Target.FechaCarga = @FechaCarga
    WHEN NOT MATCHED THEN
        INSERT (IdProductoOrigen, Nombre, Categoria, Precio, Activo, FuenteOrigen, FechaCarga)
        VALUES (@IdProductoOrigen, @Nombre, @Categoria, @Precio, @Activo, @FuenteOrigen, @FechaCarga);
END;
GO

CREATE OR ALTER PROCEDURE sp_UpsertDimTiempo
    @TiempoKey INT, @Fecha DATE, @Anio INT, @Trimestre INT, @Mes INT, @NombreMes VARCHAR(20), @Dia INT, @DiaSemana VARCHAR(20), @EsFinDeSemana BIT
AS BEGIN
    IF NOT EXISTS (SELECT 1 FROM DimTiempo WHERE TiempoKey = @TiempoKey)
    BEGIN
        INSERT INTO DimTiempo (TiempoKey, Fecha, Anio, Trimestre, Mes, NombreMes, Dia, DiaSemana, EsFinDeSemana)
        VALUES (@TiempoKey, @Fecha, @Anio, @Trimestre, @Mes, @NombreMes, @Dia, @DiaSemana, @EsFinDeSemana);
    END
END;
GO

CREATE OR ALTER PROCEDURE sp_InsertFactVentas
    @ClienteKey INT, @ProductoKey INT, @FuenteKey INT, @TiempoKey INT, @Cantidad INT, @PrecioUnitario DECIMAL(18,2), @MontoTotal DECIMAL(18,2), @FechaCarga DATETIME
AS BEGIN
    INSERT INTO FactVentas (ClienteKey, ProductoKey, FuenteKey, TiempoKey, Cantidad, PrecioUnitario, MontoTotal, FechaCarga)
    VALUES (@ClienteKey, @ProductoKey, @FuenteKey, @TiempoKey, @Cantidad, @PrecioUnitario, @MontoTotal, @FechaCarga);
END;
GO
