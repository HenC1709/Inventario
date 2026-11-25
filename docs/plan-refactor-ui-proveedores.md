# Plan de Refactor Visual y Pivote a Proveedores — StockFlow (`TiendaBodegas`)

> **Estado (2026-11-24):** **100% completado** — MVP 4 (`R0`–`R10`), extensiones **`N1`–`N3`** (Tickets 80 mm, PDF QuestPDF, Excel ClosedXML) y **MVP 5 (`N4`–`N7`)** (SQLite, `%LOCALAPPDATA%\StockFlow`, backup/restore, Serilog, empaquetado `StockFlow.exe` e Inno Setup, capturas y versionado `docs/`) cerrados (`0` warnings/errores, `65/65` pruebas pasando).  
> **Base actual:** rama `main` en `cc10b11` (PR #4 `refactor/visual-wpf` mergeado sobre MVP 3 `2fdebeb`).  
> **Fuentes de referencia:** `referencia/referencia.md`, `referencia/referencia-ui.jsx` y capturas visuales de diseño (carpeta temporal `referencia/` retirada en `R10` tras completar la implementación).

---

## 1. Contexto y alcance real

Aunque `referencia/referencia.md` describe inicialmente un *refactor solo visual* sin cambios de modelo, incorporar el cambio solicitado de **Bodegas → Proveedores** (tal como aparece en las capturas) impacta directamente el dominio y la persistencia:

| Aspecto | Estado actual | Estado objetivo |
|---|---|---|
| Entidad principal de origen | `Warehouse` (múltiples bodegas, `bodegas.json`) | `Supplier` (directorio de proveedores, `proveedores.json`) |
| Alcance del almacén | Multi-bodega con stock por bodega | Almacén único con stock global y proveedor por producto |
| Movimientos | `Entry`, `Exit` y `Transfer` entre bodegas | `Entry` (reposición/compra) y `Exit` (venta/salida); se retiran traslados |
| Campos de `Product` | `Id`, `Name`, `SKU`, `Price`, `MinStock` | `Id`, `SKU`, `Name`, `Category`, `SupplierId`, `PurchasePrice`, `SalePrice`, `MinStock` |
| Navegación lateral | `Bodegas`, `Productos`, `Stock por Bodega`, `Movimientos` | `DASHBOARD`, `INVENTARIO`, `PROVEEDORES`, `VENTAS`, `REPORTES`, `AJUSTES` |

---

## 2. Sistema visual y tokens WPF objetivo

Todo el diseño se centralizará en diccionarios de recursos (`Themes/Colors.xaml` y `Themes/Controls.xaml`) cargados desde `App.xaml` antes de rediseñar las vistas.

### Paleta de colores

| Rol | Token / Referencia | Hex |
|---|---|---|
| Fondo principal de ventana | `pearl-100` (degradado `#FDFCFB` → `#F3EFEA`) | `#F9F7F4` |
| Fondo de sidebar | `sidebar-pearl` (`#FCFBF9` → `#F4F0EB`) | `#FCFBF9` / `#F4F0EB` |
| Tarjetas, tablas e inputs | Surface blanco | `#FFFFFF` |
| Bordes suaves / divisores | `pearl-300` / `pearl-200` | `#E5DFD7` / `#F2EEE9` |
| Texto principal / secundario | Stone 900 / 700 / 500 | `#1C1917` / `#44403C` / `#78716C` |
| Ítem activo sidebar y alerta KPI | `warm-100` (fondo) / `warm-200` (borde) | `#FDE8D8` / `#F9D1B3` |
| Indicador lateral activo (4 px) y acento | `warm-500` / `warm-600` / `warm-700` | `#C86D44` / `#A6522E` / `#843E20` |
| Botón primario frío y hover | `cool-600` / `cool-700` | `#1E4E79` / `#163B5C` |
| Botón secundario frío | `cool-50` (borde `cool-200`, texto `cool-700`) | `#EFF5FA` / `#BDD3E8` / `#163B5C` |
| Botones icono de acciones en tabla | `cool-100` | `#DCE8F3` |
| Badge `En Stock` / `Activo` | Verde esmeralda | Fondo `#D1FAE5` · Texto `#065F46` |
| Badge `Bajo Stock` / `En revisión` | Cálido durazno (`warm-100`) | Fondo `#FDE8D8` · Borde `#F9D1B3` · Texto `#843E20` |
| Badge `Sin Stock` | Rojo suave | Fondo `#FEE2E2` · Texto `#B91C1C` |

### Componentes clave de layout
1. **Sidebar izquierdo:** ancho fijo limpio, texto en mayúsculas, indicador vertical terracota (`4px`) en la vista activa, badge numérico en `INVENTARIO` y bloque de usuario `Admin / Usuario` al pie.
2. **Tarjetas KPI superiores:** 4 tarjetas con `CornerRadius="16"` (primera con borde terracota de foco y segunda con fondo durazno suave para alertas).
3. **Barra de herramientas:** buscador a la izquierda, `ComboBox` de filtros (`Categoría`, `Estado`, `Proveedor`) y botones de acción fríos alineados a la derecha.
4. **Tabla de datos:** encabezados limpios ordenables, píldoras semánticas de estado, botones icono compactos (`Editar` / `Eliminar`) por fila y pie de tabla con contador y paginación.

---

## 3. Plan de tareas por prioridad

### AHORA — Refactor visual + pivote Bodegas → Proveedores

- [x] **R0. Resguardo previo en Git**
  - Confirmado: commit `2fdebeb` (MVP 3, `0` warnings/errores y `31/31` pruebas pasando) resguardado en `main` y como base de la rama `refactor/visual-wpf`.
  - Confirmado: `referencia/` ya figura en `.gitignore` (regla verificada); queda únicamente el cambio local `docs/` → `docs` en `.gitignore` pendiente de decisión del usuario.

- [x] **R1. Sistema de tokens y tema global WPF (`Themes/`)**
  - Crear `src/TiendaBodegas.UI/Themes/Colors.xaml` con toda la paleta perlada, cálida, fría y semántica.
  - Crear `src/TiendaBodegas.UI/Themes/Controls.xaml` con estilos reutilizables: tarjetas KPI, botones primarios/secundarios/icono, `TextBox`, `ComboBox`, `DatePicker`, píldoras de estado, `DataGrid` y ventanas modales.
  - Cargar ambos diccionarios en `src/TiendaBodegas.UI/App.xaml`.

- [x] **R2. Cambio de dominio Bodegas → Proveedores (`Domain` + `Application` + `Infrastructure` + `Tests`)**
  - Reemplazar `Warehouse` / `IWarehouseRepository` / `WarehouseRepository` (`bodegas.json`) por `Supplier` (`Id`, `Code`, `Name`, `Contact`, `MainCategory`, `Status` — enum `SupplierStatus`) + `ISupplierRepository` + `SupplierRepository` (`proveedores.json`).
  - Ampliar `Product` con `Category`, `SupplierId`, `PurchasePrice` y `SalePrice`, manteniendo `MinStock` y compatibilidad de deserialización con `productos.json` existentes.
  - Simplificar `StockMovement`, `IInventoryService` e `InventoryManager` a un almacén único (`Entry` y `Exit`), retirando traslados multi-bodega y conservando mensajes de validación en español.
  - Actualizar `ICatalogService`, `CatalogManager`, `JsonDataContext`, datos semilla en `DependencyInjection.cs` y toda la suite xUnit hasta lograr compilación sin warnings y pruebas en verde.

- [x] **R3. Shell principal y Sidebar (`MainWindow` + `MainViewModel`)**
  - Aplicar fondo perlado y estructura de sidebar + área principal en `MainWindow.xaml`.
  - Configurar `MainViewModel` para navegar entre las 6 secciones (`DASHBOARD`, `INVENTARIO`, `PROVEEDORES`, `VENTAS`, `REPORTES`, `AJUSTES`), exponiendo el contador dinámico para el badge de `INVENTARIO` y bloque inferior `Admin / Usuario`.

- [x] **R4. Vista `INVENTARIO` (unifica Productos + Stock)**
  - Cabecera `Inventario de Productos` con subtítulo e indicador `"Sincronizado localmente"`.
  - 4 tarjetas KPI clicables (`Total Productos` para limpiar filtros, `Bajo Stock / Agotados` para filtrar alertas, `Categorías Activas` y `Valor Estimado` en la moneda configurada).
  - Barra de herramientas con buscador en tiempo real, filtros por `Categoría`, `Estado` (`En Stock`, `Bajo Stock`, `Sin Stock`) y `Proveedor`, más botones `+ Agregar Producto` y `Exportar CSV`.
  - Tabla ordenable y paginada con columnas (`SKU`, `Producto`, `Categoría`, `Precio Compra`, `Precio Venta`, `Stock`, `Estado`, `Acciones`), encabezados dinámicos según moneda activa (`PRECIO COMPRA (COP/USD/EUR)` / `PRECIO VENTA (COP/USD/EUR)`), formato `CurrencyAmountConverter` (p. ej. `` `COP $24,500` ``) y botones icono por fila en azul `#DCE8F3`.
  - Rediseño del modal `ProductFormWindow.xaml` (2 columnas SKU/Categoría, Nombre, Proveedor, 3 columnas Precio Compra / Precio Venta / Stock + Stock Mínimo con parseo de separadores de miles colombianos en `COP`) y diálogo de confirmación estilizado.

- [x] **R5. Vista `PROVEEDORES` (CRUD completo)**
  - Sustituir `WarehousesView` / `WarehousesViewModel` por `SuppliersView` / `SuppliersViewModel`.
  - Tabla con `ID` (`PRV-01`), `Razón Social`, `Contacto`, `Categoría Principal`, `Estado` (`Activo` / `En revisión`) y acciones de edición/eliminación.
  - Modal `SupplierFormWindow` con `INotifyDataErrorInfo` y bloqueo de eliminación si existen productos vinculados al proveedor.

- [x] **R6. Vista `VENTAS` (Movimientos: Salidas/Ventas y Entradas)**
  - Rediseñar `MovementsView` / `MovementsViewModel` y `MovementFormWindow` al estilo de la captura (`Registro de Ventas y Salidas`), listando tarjetas/filas limpias con fecha, unidades, producto y monto total, manteniendo filtros por fecha/tipo y registro validado de `Salida / Venta` y `Entrada`.

---

### NECESARIO — Pantallas complementarias y camino a release

- [x] **R7. Vista `DASHBOARD`**
  - Panel `Distribución de Stock por Categoría` con barras de progreso horizontales en terracota (`TerracottaProgressBarStyle`) y acceso directo `Ir al inventario →`.
  - Panel `Alertas de Reposición` en tono cálido `warm-50` con productos críticos y botón `Revisar productos críticos` que navega al inventario filtrado.

- [x] **R8. Vista `REPORTES` y exportaciones**
  - Pantalla de reportes con tarjetas de acción (`Valoración de Inventario` con descarga CSV real y `Productos con Rotación Baja` sin movimientos en los últimos 30 días + vista previa y descarga CSV).
  - Pendiente para fase posterior: generación/impresión de tickets `FlowDocument` 80 mm (**N1**), exportación PDF con QuestPDF (**N2**) y reportes Excel con ClosedXML (**N3**).

- [x] **R9. Vista `AJUSTES`**
  - Formulario de preferencias persistido en `settings.json` (`AppSettings` / `ISettingsService`): `Nombre del Negocio / Almacén`, `Moneda Principal` (`USD`, `COP`, `EUR`) con conversión automática de precios a la tasa del día (`IExchangeRateService` / `DailyExchangeRateService` con caché diario y respaldo local sin conexión) y `Umbral de Bajo Stock` por defecto con propagación en vivo a inventario, ventas y formulario de producto.

- [x] **R10. Cierre de pruebas, limpieza y documentación**
  - Documentación principal sincronizada (`README.md`, `docs/Progress.md`, `docs/plan-refactor-ui-proveedores.md`, `docs/camino-a-sql.md`).
  - Barrido final de `50/50` pruebas automatizadas pasando, incluyendo flujo integral E2E del MVP 4 y guardia de fronteras Clean Architecture en todos los ViewModels.
  - Carpeta temporal `referencia/` eliminada tras completar la implementación y validación visual.

- [x] **N1. Tickets imprimibles 80 mm (extensión de R8)**
  - `IPrinterService` en `Application` y `WpfPrinterService` en `UI` con armado de `FlowDocument` (~278 DIPs, `PagePadding` mínimo y `ColumnWidth == PageWidth`) separado de `PrintDialog` para pruebas unitarias limpias.

- [x] **N2. Exportación PDF con QuestPDF (extensión de R8)**
  - `IPdfExportService` en `Application` y `QuestPdfExportService` en `Infrastructure` (`QuestPDF` con `LicenseType.Community` configurada una única vez en arranque/DI), reutilizando DTOs existentes y `CurrencyFormatter`.

- [x] **N3. Reportes Excel con ClosedXML (extensión de R8)**
  - `IExcelExportService` en `Application` y `ClosedXmlExcelExportService` en `Infrastructure` (`ClosedXML 0.105.1`), generando libros `.xlsx` formateados (`Valoración`, `Inventario`, `Rotación Baja`) con celdas numéricas reales, formato de moneda activo, totales y colores semánticos sin depender de fuentes de escritorio (`AdjustToContents`), integrados en `ReportsViewModel`/`ReportsView` e `InventoryViewModel`/`InventoryView`.

- [x] **N4. Migración a SQLite (MVP 5 — `docs/camino-a-sql.md`)**
  - `SqliteDataContext` (`tiendabodegas.db`, `Pooling=false`, `PRAGMA foreign_keys=ON`), `SqliteSchemaMigrator` (`SchemaVersions` v1 con `Suppliers`, `Products`, `StockMovements`, `Settings` y claves foráneas `ON DELETE RESTRICT`), repositorios SQLite detrás de `ISupplierRepository`/`IProductRepository`/`IStockMovementRepository`/`ISettingsRepository` y `JsonToSqliteMigrator` transaccional con conciliación de conteos/saldos y preservación de archivos JSON como respaldo.

- [x] **N5. Ruta en `%LOCALAPPDATA%\StockFlow`, backup y observabilidad (MVP 5)**
  - `AppDataPaths` (`%LOCALAPPDATA%\StockFlow` con promoción no destructiva de datos heredados), `IDatabaseBackupService`/`SqliteBackupService` (respaldo y restauración verificada con copia preventiva automática y UI en `Ajustes`), `IAppLogger`/`SerilogAppLogger` (`logs/stockflow-*.log`) y captura global de excepciones en `App.xaml.cs`.

- [x] **N6. Empaquetado y release (MVP 5)**
  - Binario `StockFlow.exe` (`AssemblyName=StockFlow`), instalador Inno Setup (`installer/StockFlow.iss`), script `scripts/publish-release.ps1`, capturas reales en `docs/screenshots/*.png` y `README.md` de release.

- [x] **N7. Decisión de versionado de `docs/` en `.gitignore`**
  - Retirada la exclusión `docs` de `.gitignore` para versionar documentación y capturas junto al código, manteniendo ignorados `bin/`, `obj/`, `artifacts/`, `publish/`, `installer/output/`, `*.log` y `*.docx`.

---

### ESPERA — Backlog diferido

- [x] **E1.** Retirado el código heredado sin uso (`Shipment`, `ShipmentStatus`, `DispatchManager`, `ShipmentPlanDto` y `PrinterService`).
- [ ] **E2.** Soporte multi-bodega / sucursales o motor SQL cliente-servidor si se requiere en el futuro.
- [ ] **E3.** Autenticación, usuarios y roles reales (por ahora el pie `Admin / Usuario` es informativo).
- [ ] **E4.** Lectura y generación de códigos de barras / QR.
- [ ] **E5.** Gráficos estadísticos avanzados en el Dashboard.
- [ ] **E6.** Versión web.

---

## 4. Decisiones confirmadas

1. **Almacén único y movimientos:** se retiran los **Traslados** multi-bodega; el sistema operará sobre un **único almacén** manteniendo **Entradas (compras/reposición)** y **Salidas (ventas/ajustes)**.
2. **Modelo de `Product` enriquecido:** se incorporarán `Category`, `SupplierId`, `PurchasePrice` (Precio Compra) y `SalePrice` (Precio Venta), manteniendo `MinStock`.
3. **Acciones por fila en Inventario:** se implementarán botones icono compactos de **Editar** y **Eliminar** en cada fila con fondo `#DCE8F3` (`cool-100`), fieles a la referencia visual.
4. **Orden de ejecución por necesidad técnica:** se avanzará de manera incremental respetando dependencias reales: `R0` (resguardo) → `R1` (tokens/tema global WPF) → `R2` (pivote de dominio Bodegas → Proveedores + pruebas en verde) → `R3` (shell y sidebar) → `R4` (`INVENTARIO`) → `R5` (`PROVEEDORES`) → `R6` (`VENTAS`), continuando después con el bloque **NECESARIO** (`R7–R10` y `N4–N7`).

---

## 5. Convenciones activas a respetar

- **Arquitectura:** Clean Architecture + MVVM estricta (`code-behind` sin lógica de negocio; ViewModels consumen solo interfaces de `Application`/`Domain` e `IDialogService`).
- **Idioma y documentación:** identificadores en inglés con comentarios XML (`/// <summary>`) en clases, constructores y métodos; textos visibles de UI, mensajes de error y documentación en español.
- **Compilación y pruebas:** `dotnet build TiendaBodegas.slnx` con `0` warnings/errores y `dotnet test TiendaBodegas.slnx` al 100% en cada bloque cerrado.
