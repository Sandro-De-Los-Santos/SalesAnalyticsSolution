# SalesAnalyticsSolution — Data Warehouse ETL

## Descripción del Proyecto
Solución de análisis de ventas que implementa un proceso ETL completo para la carga de dimensiones en un Data Warehouse con esquema en estrella.

## Estructura de la Solución

```
SalesAnalyticsSolution/
├── 01_Api/
│   └── SalesAnalytics.Api/         ← Web API para disparar ETL y consultar DW
│       ├── Controllers/
│       │   ├── EtlController.cs    ← POST /api/etl/ejecutar
│       │   └── DataWarehouseController.cs ← GET /api/dw/resumen
│       └── Program.cs
├── 02_Data/
│   └── ETL.Core/                   ← Biblioteca central de lógica ETL
│       ├── Models/
│       │   ├── DimCliente.cs       ← Dimensión Clientes (DW)
│       │   ├── DimProducto.cs      ← Dimensión Productos (DW)
│       │   ├── DimFuenteDatos.cs   ← Dimensión Fuentes (DW)
│       │   ├── DimTiempo.cs        ← Dimensión Tiempo (DW)
│       │   ├── FactVentas.cs       ← Tabla de Hechos (DW)
│       │   ├── Cliente.cs          ← Modelo ODS
│       │   ├── Producto.cs         ← Modelo ODS
│       │   └── Venta.cs            ← Modelo ODS
│       ├── Transform/
│       │   ├── ClienteTransformer.cs
│       │   ├── ProductoTransformer.cs
│       │   ├── VentaTransformer.cs
│       │   └── DimensionTransformer.cs ← Transformación a Dimensiones DW
│       ├── Data/
│       │   ├── Repository.cs       ← Acceso a datos ODS + DW
│       │   └── DatabaseConnection.cs
│       └── EtlRunner.cs            ← Orquestador del proceso ETL
└── 03_Worker/
    └── ETL.App/                    ← Worker Service (Background Service)
        ├── Worker.cs               ← Ejecuta el ETL periódicamente
        └── CsvFiles/               ← Archivos CSV de entrada
            ├── customers.csv
            ├── products.csv
            ├── orders.csv
            └── order_details.csv
```

## Prerrequisitos

1. **.NET 9 SDK** o superior
2. **SQL Server** (LocalDB, Express o SQL Server completo)
3. **Visual Studio 2022** v17+ o VS Code con extensión C#

## Configuración de la Base de Datos

1. Abrir SQL Server Management Studio (SSMS)
2. Crear la base de datos:
   ```sql
   CREATE DATABASE SalesAnalyticsDB;
   ```
3. Ejecutar el script completo:
   ```
   script_datawarehouse.sql
   ```
   Este script crea:
   - Tablas ODS: `TipoFuente`, `FuenteDatos`, `Categorias`, `Clientes`, `Productos`, `Ventas`, `LogCargaETL`
   - Tablas DW: `DimFuenteDatos`, `DimCliente`, `DimProducto`, `DimTiempo`, `FactVentas`
   - Stored Procedures: `sp_Insert*`, `sp_Upsert*`

## Configuración de la Aplicación

Editar `ETL.App/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=SalesAnalyticsDB;Trusted_Connection=True;"
  },
  "CsvSettings": {
    "BasePath": "CsvFiles",
    "Clientes": "customers.csv",
    "Productos": "products.csv",
    "Ordenes": "orders.csv",
    "DetalleOrdenes": "order_details.csv"
  }
}
```

## Ejecución

### Opción 1: Worker Service (ETL Automático)
```powershell
cd ETL.App
dotnet run
```

### Opción 2: Web API (Manual)
```powershell
cd SalesAnalytics.Api
dotnet run
# Luego hacer POST a: http://localhost:5000/api/etl/ejecutar
# O GET: http://localhost:5000/api/dw/resumen
```

## Verificación en Base de Datos

Después de ejecutar el ETL, correr estas consultas para verificar:

```sql
-- Resumen de dimensiones cargadas
SELECT 'DimCliente'    AS Tabla, COUNT(*) AS Registros FROM DimCliente    UNION ALL
SELECT 'DimProducto'   AS Tabla, COUNT(*) AS Registros FROM DimProducto   UNION ALL
SELECT 'DimFuenteDatos'AS Tabla, COUNT(*) AS Registros FROM DimFuenteDatos UNION ALL
SELECT 'DimTiempo'     AS Tabla, COUNT(*) AS Registros FROM DimTiempo     UNION ALL
SELECT 'FactVentas'    AS Tabla, COUNT(*) AS Registros FROM FactVentas;

-- Top 5 clientes de la dimensión
SELECT TOP 5 * FROM DimCliente;

-- Top 5 ventas con lookup de dimensiones
SELECT TOP 5
    fv.FactVentaKey,
    dc.Nombre AS Cliente,
    dp.Nombre AS Producto,
    dt.Fecha,
    dt.NombreMes,
    dt.Anio,
    fv.Cantidad,
    fv.MontoTotal
FROM FactVentas fv
JOIN DimCliente  dc ON fv.ClienteKey  = dc.ClienteKey
JOIN DimProducto dp ON fv.ProductoKey = dp.ProductoKey
JOIN DimTiempo   dt ON fv.TiempoKey   = dt.TiempoKey;
```

## Enlace al Repositorio GitHub

> **TODO**: Publicar el repositorio en GitHub y actualizar este enlace.
>
> Pasos para publicar:
> ```powershell
> cd c:\Users\user\source\repos\SalesAnalyticsSolution
> git add .
> git commit -m "feat: implementacion completa de carga de dimensiones Data Warehouse"
> git remote add origin https://github.com/TU_USUARIO/SalesAnalyticsSolution.git
> git push -u origin main
> ```
