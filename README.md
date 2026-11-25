# TiendaBodegas

Aplicación de escritorio Windows (.NET 10 / WPF) para gestionar inventario en almacén único, catálogo de productos, directorio de proveedores, entradas/salidas de stock, reportes CSV y configuración multimoneda (`USD`, `COP`, `EUR`) con Clean Architecture y MVVM.

## Estructura

- `src/TiendaBodegas.Domain`: entidades (`Product`, `Supplier`, `StockMovement`, `Inventory`, `AppSettings`), enumeraciones (`MovementType`, `SupplierStatus`) e interfaces de repositorio.
- `src/TiendaBodegas.Application`: servicios de aplicación (`InventoryManager`, `CatalogManager`, `SettingsManager`, `InventoryCsvExporter`), DTOs, validaciones de negocio e interfaces (`IInventoryService`, `ICatalogService`, `ISettingsService`, `IExchangeRateService`, `IInventoryExportService`).
- `src/TiendaBodegas.Infrastructure`: persistencia JSON atómica (`JsonDataContext`), repositorios (`SupplierRepository`, `ProductRepository`, `StockMovementRepository`, `SettingsRepository`) y servicio de tasa de cambio diaria (`DailyExchangeRateService`).
- `src/TiendaBodegas.UI`: aplicación de escritorio WPF (`net10.0-windows`) con MVVM (`CommunityToolkit.Mvvm`), diccionarios de tema (`Themes/`), convertidores (`Converters/`) e inyección de dependencias (`Microsoft.Extensions.DependencyInjection`).
- `tests/TiendaBodegas.Tests`: suite de pruebas automatizadas con `xUnit`.

El código usa identificadores y comentarios XML en inglés; la interfaz y la documentación del proyecto están en español.

## Estado actual (MVP 3 + MVP 4 completados al 100% — `R0`–`R10` cerrados)

- **Rama actual:** `main` (`cc10b11`, PR #4 `refactor/visual-wpf` mergeado).
- **Dominio y persistencia JSON:** pivote de múltiples bodegas a **almacén único + Proveedores (`Supplier`)**. `JsonDataContext` persiste de forma atómica `proveedores.json`, `productos.json`, `tickets.json` y `settings.json`, conservando compatibilidad legado con `"Precio"` y filtrando registros históricos de traslados. En primer arranque sin datos siembra `4` proveedores, `12` productos y `13` movimientos.
- **Sistema visual y shell WPF:** tema perlado/terracota/azul frío centralizado en `Themes/Colors.xaml` y `Themes/Controls.xaml`, con barra lateral de 6 secciones (`DASHBOARD`, `INVENTARIO` con contador dinámico, `PROVEEDORES`, `VENTAS`, `REPORTES`, `AJUSTES`) e icono `assets/Inventory.ico`.
- **Vistas operativas (`R3`–`R9`):**
  - `DASHBOARD`: distribución de stock por categoría en barras horizontales y panel `Alertas de Reposición` enlazado al inventario filtrado.
  - `INVENTARIO`: 4 tarjetas KPI clicables, buscador en tiempo real, filtros (`Categoría`, `Estado`, `Proveedor`), ordenamiento multi-página, paginación dinámica adaptada al alto de pantalla (mínimo 6 filas), exportación CSV (RFC-4180), modal `ProductFormWindow` y confirmación `ConfirmDialogWindow`.
  - `PROVEEDORES`: directorio `PRV-XX` (`Activo` / `En revisión`), modal `SupplierFormWindow` con `INotifyDataErrorInfo` y bloqueo de eliminación si existen productos vinculados.
  - `VENTAS`: historial en tarjetas limpias con fechas relativas (`Hoy` / `Ayer`), filtros por rango de fechas y tipo (`Entrada` / `Salida`), y modal `MovementFormWindow`.
  - `REPORTES`: descarga CSV de valoración por categoría (costo de compra, valor de venta y margen) y auditoría de rotación baja (30 días sin movimientos) con vista previa y exportación CSV.
  - `AJUSTES` y moneda diaria: preferencias persistidas en `settings.json` (`NombreNegocio`, `MonedaPrincipal` `USD`/`COP`/`EUR`, `UmbralBajoStock`). Al cambiar de moneda, `DailyExchangeRateService` consulta la tasa del día (`open.er-api.com` con failover a `currency-api` en jsDelivr, caché por día y respaldo local sin conexión), convierte precios con `Product.ConvertPrices` y propaga en vivo encabezados (`PRECIO COMPRA (COP)`), montos (`CurrencyAmountConverter`, ej. `` `COP $24,500` `` o `` `$12.00` ``) y parseo de separadores de miles colombianos en `ProductFormWindow`.
- **Pruebas automatizadas:** `50/50` pruebas xUnit pasando en `9` clases (`CatalogManagerTests`, `DashboardReportsSettingsTests`, `InventoryManagerTests`, `InventoryViewModelTests`, `JsonDataContextTests`, `Mvp1ViewModelsAndDiTests`, `Mvp2CrudViewModelsTests`, `Mvp3MovementsViewModelsTests`, `ThemeResourcesTests`) con `0` advertencias y `0` errores.

## Compilación, ejecución y pruebas

La solución está definida en `TiendaBodegas.slnx` (`net10.0` en backend/infraestructura y `net10.0-windows` en UI/pruebas).

```powershell
# Compilar toda la solución
dotnet build TiendaBodegas.slnx

# Ejecutar la aplicación WPF
dotnet run --project src/TiendaBodegas.UI

# Ejecutar pruebas automatizadas
dotnet test TiendaBodegas.slnx
```

---

## Tareas pendientes

### Próximas etapas — NECESARIO (`N1`–`N7`)
- [x] **N1. Tickets imprimibles (80 mm):** `IPrinterService` + `WpfPrinterService` con armado de `FlowDocument` (~278 DIPs) desacoplado de `PrintDialog`.
- [x] **N2. Exportación PDF:** `IPdfExportService` + `QuestPdfExportService` con QuestPDF (`LicenseType.Community`), reutilizando DTOs y formato de moneda activo.
- [ ] **N3. Reportes Excel:** exportación `.xlsx` con ClosedXML.
- [ ] **N4. Migración a SQLite (MVP 5):** esquema relacional (`Suppliers`, `Products`, `StockMovements`, `Settings`), repositorios SQL, migraciones versionadas y migrador transaccional desde JSON (ver `docs/camino-a-sql.md`).
- [ ] **N5. Almacenamiento en usuario, backup y observabilidad (MVP 5):** evaluar traslado de `data/` a `%LOCALAPPDATA%\TiendaBodegas`, copia de seguridad/restauración, logging con Serilog y manejo global de excepciones.
- [ ] **N6. Empaquetado y release (MVP 5):** pulido final de escritorio, instalador Windows (Inno Setup o MSIX) y `README.md` final con capturas.
- [ ] **N7. Versionado de `docs/`:** decidir si `docs/` permanece ignorado localmente en `.gitignore` o pasa a versionarse en Git.

### Backlog diferido — ESPERA (`E1`–`E6`)
- [ ] **E1.** Decidir si se retira o implementa el código heredado sin usar (`Shipment`, `DispatchManager`, `ShipmentPlanDto` y `PrinterService`).
- [ ] **E2.** Soporte multi-bodega / sucursales o motor SQL cliente-servidor.
- [ ] **E3.** Autenticación, usuarios y roles reales (el pie `Admin / Usuario` es actualmente informativo).
- [ ] **E4.** Lectura y generación de códigos de barras / QR.
- [ ] **E5.** Gráficos estadísticos avanzados en el Dashboard.
- [ ] **E6.** Versión web.

## Convenciones activas del proyecto

- **Arquitectura:** Clean Architecture + MVVM estricta (`code-behind` sin lógica de negocio; ViewModels consumen solo interfaces de `Application`/`Domain` e `IDialogService`).
- **Idioma y documentación:** identificadores en inglés con comentarios XML (`/// <summary>`) en clases, constructores y métodos; UI y documentación en español; conservar las claves `[JsonPropertyName]` existentes.
- **Notas WPF:** al usar `<UseWPF>true</UseWPF>`, importar explícitamente `using System.IO;` en UI y pruebas cuando se utilicen `Path`, `File` o `Directory`.
