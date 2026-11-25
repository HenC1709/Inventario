# Progress — StockFlow (`TiendaBodegas`)

Bitácora de avance del proyecto. Sirve para retomar el trabajo sin perder contexto.
Última actualización: 2026-11-24 · Rama actual: `feat/r8-print-and-pdf-export` (sobre `main` `cc10b11`).

Fuente de requerimientos: `TiendaBodegas — Visión, Roadmap y MVPs (WPF).docx` y `docs/plan-refactor-ui-proveedores.md` (ignorados en Git).

---

## 1. Resumen ejecutivo

| Etapa | Estado | Rama / PR | Pruebas |
|-------|--------|-----------|---------|
| Paso 0 — Estabilizar backend | Completado | `refactor/MVP0` (PR #1) | 9/9 |
| MVP 1 — Base WPF | Completado | `refactor/MVP0` (PR #1) | 11/11 |
| MVP 2 — CRUD y formularios | Completado | `feature/mvp2-crud-formularios` (PR #2) | 19/19 |
| MVP 3 — Movimientos de inventario | Completado | `feature/mvp3-movimientos-inventario` (PR #3) | 31/31 |
| MVP 4 — Refactor visual, Proveedores y Moneda diaria | Completado (`R0`–`R10`, `11/11`) | `refactor/visual-wpf` (PR #4) | 50/50 |
| Extensiones de documentos (`N1`–`N3`: Ticket 80 mm, PDF, Excel) | **Completado** (`N1`, `N2` y `N3`) | `feat/r8-print-and-pdf-export` | 61/61 |
| MVP 5 — SQLite, observabilidad y release (`N4`–`N7`) | **Completado** (`N4`, `N5`, `N6` y `N7`) | `feat/r8-print-and-pdf-export` | 65/65 |

Última verificación conocida: `dotnet build TiendaBodegas.slnx` con `0` errores y `0` advertencias; `dotnet test TiendaBodegas.slnx --no-build` con `65/65` pruebas superadas (2026-11-24).

---

## 2. Arquitectura actual

Clean Architecture; la UI (WPF + MVVM) solo habla con `Application`/`Domain` y con `IDialogService` (sin acceso directo a repositorios ni `JsonDataContext`).

| Proyecto | Target | Contenido |
|----------|--------|-----------|
| `src/TiendaBodegas.Domain` | `net10.0` | Entidades `Product` (con `Category`, `SupplierId`, `PurchasePrice`, `SalePrice`, `MinStock`, compatibilidad legado `"Precio"` y `ConvertPrices`), `Supplier`, `StockMovement`, `Inventory`, `AppSettings`, `Shipment` (legado); enums `MovementType` (`Entry`, `Exit`), `SupplierStatus` (`Active`, `UnderReview`), `ShipmentStatus`; interfaces `IRepository<T>`, `IProductRepository`, `ISupplierRepository`, `IStockMovementRepository`, `ISettingsRepository` |
| `src/TiendaBodegas.Application` | `net10.0` | `IInventoryService`/`InventoryManager`, `ICatalogService`/`CatalogManager`, `ISettingsService`/`SettingsManager`, `IInventoryExportService`/`InventoryCsvExporter`, `IExcelExportService`, `IPdfExportService`, `IPrinterService`, `IExchangeRateService`, `IDatabaseBackupService`, `IAppLogger`; DTOs (`ProductStockSummaryDto`, `StockMovementDisplayDto`, `StockMovementFilter`, `StockMovementRequest`, `CategoryValuationDto`, `LowRotationProductDto`, `ExchangeRateQuote`, `SettingsUpdateResult`) |
| `src/TiendaBodegas.Infrastructure` | `net10.0` | `AppDataPaths`, `SqliteDataContext`, `SqliteSchemaMigrator`, `SqliteBackupService`, `JsonToSqliteMigrator`, repositorios SQLite (`SqliteSupplierRepository`, `SqliteProductRepository`, `SqliteStockMovementRepository`, `SqliteSettingsRepository`), `JsonDataContext` y repositorios JSON de respaldo/migración, `ClosedXmlExcelExportService`, `QuestPdfExportService`, `DailyExchangeRateService`, `SerilogAppLogger` |
| `src/TiendaBodegas.UI` | `net10.0-windows` (WPF, `WinExe` → `StockFlow.exe`) | `App` (captura global de excepciones), `DependencyInjection`, `MainWindow`, `Themes/` (`Colors.xaml`, `Controls.xaml`), `Converters/` (`MovementTextFormatter.cs`, `CurrencyAmountConverter.cs`), `WpfPrinterService`, ViewModels (`Main`, `Dashboard`, `Inventory`, `Suppliers`, `Movements`, `Reports`, `Settings`, modales), Vistas XAML, `IDialogService`/`DialogService`, `assets/Inventory.ico` |
| `tests/TiendaBodegas.Tests` | `net10.0-windows` | xUnit; `13` clases de prueba (`65` tests) |

Paquetes NuGet principales: `CommunityToolkit.Mvvm 8.4.0`, `Microsoft.Extensions.DependencyInjection 9.0.0`, `Microsoft.Data.Sqlite 10.0.15`, `ClosedXML 0.105.1`, `QuestPDF 2026.9.2`, `Serilog 4.4.0`, `Serilog.Sinks.File 7.0.0`, xUnit (`xunit 2.9.2`, `Microsoft.NET.Test.Sdk 17.12.0`, `xunit.runner.visualstudio 2.8.2`, `coverlet.collector 6.0.2`).

### Persistencia
- Motor activo: SQLite local en `%LOCALAPPDATA%\StockFlow\tiendabodegas.db` (`AppDataPaths.ResolveDataDirectory` + `SqliteDataContext` con `Pooling = false` y `PRAGMA foreign_keys = ON`). Esquema versionado en `SchemaVersions` (v1: `Suppliers`, `Products`, `StockMovements`, `Settings`) con claves foráneas `ON DELETE RESTRICT`.
- Promoción y migración automática: si `%LOCALAPPDATA%\StockFlow` está vacío, `AppDataPaths` promueve datos heredados desde `bin/.../data/` o `%LOCALAPPDATA%\TiendaBodegas`; luego `JsonToSqliteMigrator` importa `.json` previos dentro de una transacción conciliando conteos y saldos netos por producto, conservando los archivos de origen como respaldo.
- Respaldos y diagnósticos: `SqliteBackupService` crea/restaura copias verificadas (`PRAGMA integrity_check` / `quick_check` + respaldo preventivo en `backups/pre-restore-*.db`) y `SerilogAppLogger` escribe logs diarios UTF-8 en `logs/stockflow-*.log`.

### Flujo de arranque
`App.OnStartup` → `AddTiendaBodegasServices()` (configura QuestPDF Community una sola vez y registra SQLite + servicios) → `InitializeDatabase` (migra esquema SQLite `v1`, ejecuta `JsonToSqliteMigrator.MigrateIfNeeded`, y si no hay proveedores ni productos siembra `4` proveedores, `12` productos y `13` movimientos) → muestra `MainWindow` (por defecto en `INVENTARIO`). `App.OnExit` → dispone el contenedor DI sin sobrescribir archivos JSON de respaldo.

---

## 3. Qué se hizo en cada etapa

### Paso 0, MVP 1 y MVP 2 (base cerrada en `main`)
- Estabilización del backend JSON (`[JsonInclude]`, `AssignId`, escritura atómica, validaciones base de `InventoryManager`), conversión de UI a WPF con DI y MVVM, CRUD de catálogo con `INotifyDataErrorInfo` y borrado protegido si existían movimientos (`19/19` pruebas).

### MVP 3 — Movimientos, stock mínimo y alertas (`31/31` pruebas)
- Incorporación de `MinStock` en `Product`, filtros de historial (`StockMovementFilter`), registro unificado (`StockMovementRequest`), vistas y modales de movimientos con errores de negocio en español y desacoplamiento total de la UI respecto a repositorios.

### MVP 4 — Refactor visual, pivote a Proveedores y Moneda diaria (`R0`–`R10`, `50/50` pruebas)
- **`R0`–`R1`:** Resguardo base en Git (`2fdebeb`) y sistema de tokens visuales en `Themes/Colors.xaml` y `Themes/Controls.xaml` (paleta perlada/terracota/azul frío, barras terracota y corrección de `ContentTemplateSelector` en `ModernComboBoxStyle`).
- **`R2`:** Pivote de dominio `Warehouse` → `Supplier` (almacén único con `proveedores.json`), `Product` enriquecido (`Category`, `SupplierId`, `PurchasePrice`, `SalePrice`, `MinStock`) y movimientos simplificados `Entry` / `Exit`.
- **`R3`:** Shell en `MainWindow` con sidebar de 6 secciones (`DASHBOARD`, `INVENTARIO` con badge dinámico, `PROVEEDORES`, `VENTAS`, `REPORTES`, `AJUSTES`), pie `Admin / Usuario` e icono `assets/Inventory.ico`.
- **`R4`–`R6`:** Vista unificada `INVENTARIO` (4 KPIs clicables, búsqueda en tiempo real, 3 filtros, ordenamiento multi-página, paginación de 6 filas, exportación CSV RFC-4180, modal `ProductFormWindow` y confirmación `ConfirmDialogWindow`); vista `PROVEEDORES` (CRUD `PRV-XX` con bloqueo si hay productos asociados) y vista `VENTAS` (tarjetas limpias con fechas relativas vía `MovementTextFormatter`).
- **`R7`–`R9` + Moneda `COP` y tasa del día:** `DASHBOARD` con barras por categoría y alertas críticas; `REPORTES` con valoración por categoría CSV y auditoría de baja rotación (30 días); `AJUSTES` en `settings.json` (`USD`, `COP`, `EUR`, reemplazando `MXN`) + `DailyExchangeRateService` (`open.er-api.com` + failover jsDelivr + caché diario + respaldo local sin conexión) que convierte precios con `Product.ConvertPrices` / `UpdateRange` y actualiza encabezados, `CurrencyAmountConverter` (ej. `` `COP $24,500` ``, `` `$12.00` ``, `` `€11.04` ``) y parseo de miles colombianos en `ProductFormViewModel`.
- **`R10`:** Prueba integral de punta a punta del flujo completo de MVP 4, guardia arquitectónica por reflexión validando fronteras Clean Architecture/MVVM en todos los ViewModels, eliminación de la carpeta temporal `referencia/` y sincronización documental final (`README.md`, `docs/Progress.md`, `docs/plan-refactor-ui-proveedores.md`).

---

## 4. Mapa de pruebas (65 en total, 13 clases)

| Clase | Qué cubre |
|-------|-----------|
| `JsonDataContextTests` | Ida y vuelta de entidades con `private set`, migración legado `"Precio"` y escritura atómica sin `.tmp` residual |
| `InventoryManagerTests` | Entradas, salidas, stock insuficiente, filtrado por rango de fechas/tipo y rechazo de cantidades/IDs inválidos |
| `CatalogManagerTests` | CRUD de productos y proveedores, SKU único, validación de precios y borrado bloqueado por dependencias |
| `Mvp1ViewModelsAndDiTests` | Seed inicial en SQLite sin duplicados entre reinicios y navegación de 6 secciones en `MainViewModel` |
| `Mvp2CrudViewModelsTests` | Validación `INotifyDataErrorInfo` en formularios de producto/proveedor, búsqueda y manejo de errores |
| `Mvp3MovementsViewModelsTests` | Alta de entradas/salidas en `MovementFormViewModel`, filtros y fechas relativas en `MovementsViewModel` |
| `InventoryViewModelTests` | KPIs, filtros combinados, ordenamiento multi-página, paginación de 6 filas (y dinámica), exportación CSV y ajustes de stock |
| `ThemeResourcesTests` | Carga de `Colors.xaml` / `Controls.xaml` y verificación de `ContentTemplateSelector` en `ModernComboBoxStyle` |
| `DashboardReportsSettingsTests` | `DashboardViewModel`, valoración y rotación baja en `ReportsViewModel`, `DailyExchangeRateService`, conversión `USD`/`COP`/`EUR`, `CurrencyAmountConverter`, persistencia en SQLite, flujo integral E2E MVP 4 y guardia de fronteras Clean Architecture en ViewModels |
| `TicketAndPdfExportTests` | Tickets térmicos 80 mm (`WpfPrinterService`) y exportación PDF con QuestPDF (`QuestPdfExportService`) en `ReportsViewModel` y `MovementsViewModel` |
| `ExcelExportTests` | Generación `.xlsx` con ClosedXML (`Valoración`, `Inventario`, `Rotación Baja`), celdas numéricas, formato moneda y comandos Excel en `ReportsViewModel` e `InventoryViewModel` |
| `SqlitePersistenceAndMigrationTests` | Migraciones `SchemaVersions` v1, CRUD y `UpdateRange` en repositorios SQLite, integridad `ON DELETE RESTRICT`, migración transaccional JSON→SQLite con conciliación y rollback ante errores |
| `BackupObservabilityAndAppDataTests` | Resolución `%LOCALAPPDATA%\StockFlow`, promoción no destructiva de datos legados, backup/restore SQLite con copia preventiva y recarga en vivo de ViewModels, logs Serilog y `App.ProcessUnhandledException` |

---

## 5. Estado real del código (observaciones al 2026-11-24)

- El almacenamiento por defecto reside en `%LOCALAPPDATA%\StockFlow` (`AppDataPaths`), con promoción no destructiva desde rutas heredadas y rutas aisladas inyectables para pruebas.
- `.gitignore` ahora permite versionar `docs/` (incluyendo `docs/screenshots/*.png`) e ignora salidas de compilación y empaquetado (`bin/`, `obj/`, `artifacts/`, `publish/`, `installer/output/`, `*.log`, `*.docx`).
- En entornos con sandbox de Windows, `dotnet build` requiere `-m:1 -nr:false` y `dotnet test` requiere ejecución fuera del sandbox por comunicación de `vstest`/`testhost`; en máquina local directa funciona con los comandos estándar.

---

## 6. Tareas pendientes (plan de continuidad)

- [x] **R10 — Cierre MVP 4:** documentación sincronizada, barrido final de pruebas (`50/50`) y carpeta temporal `referencia/` eliminada.
- [x] **N1 — Tickets imprimibles (80 mm):** `IPrinterService` en `Application` y `WpfPrinterService` en `UI` con `FlowDocument` (~278 DIPs, `ColumnWidth == PageWidth`) separado de `PrintDialog`.
- [x] **N2 — Exportación PDF:** `IPdfExportService` + `QuestPdfExportService` con `QuestPDF` (`LicenseType.Community` configurada una única vez en DI), reutilizando DTOs y `CurrencyFormatter`.
- [x] **N3 — Reportes Excel:** `IExcelExportService` + `ClosedXmlExcelExportService` con ClosedXML (`0.105.1`), generando libros `.xlsx` formateados para valoración, inventario y auditoría de rotación baja sin `AdjustToContents()`.
- [x] **N4 — Migración a SQLite (MVP 5 — `docs/camino-a-sql.md`):** `SqliteDataContext` (`tiendabodegas.db`), `SqliteSchemaMigrator` (`SchemaVersions` v1), repositorios SQLite detrás de las interfaces de dominio y `JsonToSqliteMigrator` transaccional con conciliación y preservación de respaldos JSON.
- [x] **N5 — `%LOCALAPPDATA%\StockFlow`, backup y observabilidad (MVP 5):** `AppDataPaths`, `IDatabaseBackupService`/`SqliteBackupService` con UI en `Ajustes`, `IAppLogger`/`SerilogAppLogger` y manejo global de excepciones en `App.xaml.cs`.
- [x] **N6 — Empaquetado y release (MVP 5):** `StockFlow.exe` (`AssemblyName=StockFlow`), `installer/StockFlow.iss`, `scripts/publish-release.ps1`, capturas reales en `docs/screenshots/` y `README.md` de release.
- [x] **N7 — Versionado de `docs/`:** retirada la exclusión `docs` en `.gitignore` para versionar documentación y capturas en Git.
- [x] **E1 (limpieza):** retirados los archivos heredados sin uso (`Shipment.cs`, `ShipmentStatus.cs`, `DispatchManager.cs`, `ShipmentPlanDto.cs` y `PrinterService.cs`).
- [ ] **E2–E6 — Backlog diferido:** soporte multi-bodega o SQL servidor (**E2**), usuarios y roles reales (**E3**), códigos de barras/QR (**E4**), gráficos avanzados en Dashboard (**E5**) y versión web (**E6**).

---

## 7. Convenciones activas

- Clean Architecture + MVVM estricta; `code-behind` sin lógica de negocio; ViewModels solo consumen interfaces.
- Identificadores en inglés con comentarios XML (`/// <summary>`) en clases, constructores y métodos; documentación y textos de UI en español.
- Ramas `feature/...`, `fix/...`, `docs/...`; commits `tipo(scope): descripción`.
- Al cerrar cada bloque de trabajo en el chat: qué se encontró/cambió, cuántas tareas están completas y cuántas faltan, y una breve explicación técnica del porqué.

## 8. Comandos útiles

```powershell
dotnet build TiendaBodegas.slnx
dotnet run --project src/TiendaBodegas.UI
dotnet test TiendaBodegas.slnx
```

## 9. Historial de Git (más reciente primero)

```text
cc10b11 Merge pull request #4 from HenC1709/refactor/visual-wpf
a99891b feat(currency): soportar COP y conversión automática a tasa del día
8a6bfd1 feat: R7/R10
a67e94e feat: R4/10
ad5a55e refactor: New Interface
2fdebeb Merge pull request #3 from HenC1709/feature/mvp3-movimientos-inventario
a440555 feat: mvp 3
b07711e docs: readme update
e04f0fb Merge pull request #2 from HenC1709/feature/mvp2-crud-formularios
08a3226 feat(crud): implement product and warehouse CRUD, validation forms, and search (MVP 2)
```
