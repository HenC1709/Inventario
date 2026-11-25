# Camino a persistencia SQL

**Estado: implementado (N4 — SQLite local activo).** La aplicación opera sobre SQLite (`data/tiendabodegas.db`) mediante `Microsoft.Data.Sqlite 10.0.15`, con migración transaccional automática desde JSON legado.

## Arquitectura implementada

1. **Proveedor y alcance.** Se adoptó SQLite local (`tiendabodegas.db`) mediante `SqliteDataContext` con `Pooling = false` (evitando bloqueos de archivo en Windows), `PRAGMA foreign_keys = ON` y `PRAGMA busy_timeout = 5000`.
2. **Modelo relacional (`SchemaVersions` v1).** Gestionado por `SqliteSchemaMigrator`:
   - `Suppliers` (`Id`, `Code` único `COLLATE NOCASE`, `Name` `COLLATE NOCASE`, `Contact`, `MainCategory`, `Status` `0|1`) + índice `IX_Suppliers_Name`.
   - `Products` (`Id`, `Name`, `SKU` único `COLLATE NOCASE`, `Category`, `SupplierId` nullable con `FOREIGN KEY ... ON DELETE RESTRICT`, `PurchasePrice`, `SalePrice`, `MinStock >= 0`) + índices `IX_Products_SupplierId`, `IX_Products_Category`.
   - `StockMovements` (`Id`, `Date` ISO-8601 `"O"`, `ProductId` con `FOREIGN KEY ... ON DELETE RESTRICT`, `Quantity > 0`, `MovementType` `0|1`, `Notes`) + índices `IX_StockMovements_ProductId`, `IX_StockMovements_Date`, `IX_StockMovements_MovementType`.
   - `Settings` (`Id = 1`, `BusinessName`, `CurrencyCode`, `DefaultLowStockThreshold >= 0`).
3. **Separación de persistencia.** `SqliteSupplierRepository`, `SqliteProductRepository` (incluyendo `UpdateRange` transaccional), `SqliteStockMovementRepository` y `SqliteSettingsRepository` implementan las interfaces existentes de `TiendaBodegas.Domain.Interfaces` sin modificar la capa `Application` ni los `ViewModels`.
4. **Migración automática desde JSON.** En `DependencyInjection.InitializeDatabase`, cuando SQLite está vacío y existen archivos JSON previos (`proveedores.json`, `productos.json`, `tickets.json`, `settings.json`), `JsonToSqliteMigrator.MigrateIfNeeded` valida IDs y referencias, inserta todo en una única transacción, concilia conteos y saldos netos por producto (reviertiendo con `Rollback` ante cualquier inconsistencia) y conserva los archivos `.json` intactos como respaldo.

## Implementado en etapas posteriores (N5–N6)

- **N5:** Almacenamiento per-user en `%LOCALAPPDATA%\StockFlow` (`AppDataPaths` con promoción automática desde datos previos), copias de seguridad y restauración SQLite verificadas desde **Ajustes** (`SqliteBackupService` + copia preventiva en `backups/pre-restore-*.db`) y logs diarios en `logs/stockflow-*.log` (`SerilogAppLogger`).
- **N6:** Empaquetado `StockFlow.exe`, script `scripts/publish-release.ps1` e instalador Inno Setup `installer/StockFlow.iss`.
