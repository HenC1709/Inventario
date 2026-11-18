# TiendaBodegas

Aplicación de escritorio Windows (.NET / WPF) para gestionar bodegas, productos, movimientos de inventario y existencias por bodega con Clean Architecture y MVVM.

## Estructura

- `src/TiendaBodegas.Domain`: entidades, enumeraciones e interfaces de repositorio.
- `src/TiendaBodegas.Application`: servicios de aplicación, validaciones de negocio e interfaces.
- `src/TiendaBodegas.Infrastructure`: persistencia JSON (`JsonDataContext`), repositorios e infraestructura.
- `src/TiendaBodegas.UI`: aplicación de escritorio WPF (`net10.0-windows`) con MVVM (`CommunityToolkit.Mvvm`) e inyección de dependencias (`Microsoft.Extensions.DependencyInjection`).
- `tests/TiendaBodegas.Tests`: proyecto de pruebas automatizadas con `xUnit`.

El código usa identificadores en inglés; la documentación del proyecto está en español.

## Estado actual (MVP 2 — CRUD y formularios completado)

- **Dominio y persistencia JSON:** las entidades (`Warehouse`, `Product`, `StockMovement`, `Shipment`) cuentan con `[JsonInclude]`, `AssignId` y `Update`; `JsonDataContext` guarda bodegas, productos y movimientos de forma atómica bajo `data/` junto al ejecutable conservando las claves JSON existentes.
- **Repositorios y reglas de negocio:** `WarehouseRepository`, `ProductRepository` (con búsqueda por SKU sin distinguir mayúsculas/minúsculas y `Delete`) y `StockMovementRepository` implementados. `InventoryManager` valida cantidades, referencias y stock; `CatalogManager` (`ICatalogService`) centraliza el CRUD de productos/bodegas, unicidad de SKU, precio mayor a cero y bloqueo de eliminación si ya existen movimientos.
- **Interfaz WPF (MVP 1 y MVP 2):** `App.xaml.cs` y `DependencyInjection.cs` cargan los JSON al iniciar, siembran datos de prueba cuando la carpeta está vacía y guardan al cerrar. `MainWindow` ofrece navegación lateral entre Bodegas, Productos y Movimientos; las vistas de Productos y Bodegas incluyen buscador en tiempo real, formularios modales (`ProductFormWindow`, `WarehouseFormWindow`) validados con `INotifyDataErrorInfo`, `IDialogService` para confirmaciones y banners visibles de éxito o error.
- **Pruebas automatizadas:** `TiendaBodegas.Tests` cubre la ida y vuelta de `JsonDataContext`, las reglas de `InventoryManager`, el CRUD y borrado protegido en `CatalogManager`, el seeding/navegación del MVP 1 y las validaciones `INotifyDataErrorInfo` y filtros del MVP 2 (`19/19` pruebas pasando).
- **Pendiente para próximos MVPs:**
  - **MVP 3:** pantallas de entradas, salidas y traslados, consulta de stock por bodega, alertas de stock bajo y filtros en historial.
  - **MVP 4:** vista previa, impresión de tickets (`FlowDocument`) y exportación a PDF.
  - **MVP 5:** reportes en Excel, migración a SQLite ([docs/camino-a-sql.md](docs/camino-a-sql.md)), backup, logs e instalador.
  - Los componentes heredados de despacho/impresión (`DispatchManager`, DTOs vacíos y `PrinterService`) siguen pendientes para su MVP correspondiente.

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

## Contexto de continuidad y Plan para mañana (MVP 3 — Movimientos de inventario)

Esta sección sirve como punto de reanudación para la siguiente sesión sin perder el contexto técnico ni las convenciones del proyecto.

### Qué ya quedó cerrado hasta hoy (Paso 0, MVP 1 y MVP 2)

- **Paso 0 (Estabilización del backend):**
  - Entidades `Product`, `Warehouse`, `StockMovement` y `Shipment` con `[JsonInclude]` en propiedades `private set`, `AssignId(int id)` y `Update(...)` en `Product`/`Warehouse`.
  - `JsonDataContext` con constructor opcional (`dataDirectory`) para pruebas aisladas y escritura atómica (`.tmp` + reemplazo).
  - Repositorios `WarehouseRepository`, `ProductRepository` (incluye `GetBySku` case-insensitive y `Delete`) y `StockMovementRepository`.
  - Validaciones en `InventoryManager` (`quantity > 0`, existencia de producto/bodega, verificación de stock disponible en salidas y traslados).
- **MVP 1 (Base WPF):**
  - `TiendaBodegas.UI` convertido a `WinExe` (`net10.0-windows`, `<UseWPF>true</UseWPF>`) con `CommunityToolkit.Mvvm` y `Microsoft.Extensions.DependencyInjection`.
  - `App.xaml.cs` y `DependencyInjection.cs` cargan JSON al iniciar, siembran datos de prueba cuando `data/` está vacío y guardan al salir.
  - `MainWindow` con menú lateral (`MainViewModel`: Bodegas, Productos y Movimientos) y vistas iniciales en `DataGrid`.
- **MVP 2 (CRUD y formularios):**
  - `ICatalogService` / `CatalogManager` en `Application` con validaciones de campos requeridos, precio `> 0`, SKU único y **bloqueo de borrado** cuando un producto o bodega ya tiene movimientos (`StockMovement`).
  - `IDialogService` / `DialogService` para abrir modales y confirmaciones sin acoplar los ViewModels a ventanas concretas.
  - `ValidatableViewModel` (`INotifyDataErrorInfo`), `ProductFormViewModel` / `ProductFormWindow` y `WarehouseFormViewModel` / `WarehouseFormWindow`.
  - Buscador en tiempo real (`SearchText`), botones CRUD y banners de éxito/error en `ProductsView` y `WarehousesView`.
- **Cobertura de pruebas actual:** `19/19` pruebas pasando en `TiendaBodegas.Tests` (`JsonDataContextTests`, `InventoryManagerTests`, `Mvp1ViewModelsAndDiTests`, `CatalogManagerTests` y `Mvp2CrudViewModelsTests`).

### Plan de trabajo para mañana — MVP 3: Movimientos de inventario

1. **Paso previo en Git:** Mergear el PR de `feature/mvp2-crud-formularios` a `main`, ejecutar `git pull` y crear la rama `feature/mvp3-movimientos-inventario`.
2. **Tarea 1 — Dominio y Aplicación (umbral de stock bajo y consultas):**
   - Incorporar umbral de stock mínimo por producto (`MinStock` con `[JsonInclude]`) en `Product` (y opcionalmente editable en `ProductFormViewModel` / `CatalogManager`) manteniendo compatibilidad con JSON existentes.
   - Revisar/ampliar `IInventoryService` e `InventoryManager` para exponer de forma clara el stock por bodega (`GetStockByWarehouse`), stock total (`GetTotalStockByProduct`) y consulta de movimientos filtrables.
3. **Tarea 2 — Registro de Entradas, Salidas y Traslados en UI:**
   - Crear formulario(s) o pestaña/modal para registrar **Entrada**, **Salida** y **Traslado** (producto, bodega origen/destino según tipo, cantidad y observaciones) conectado a `IInventoryService`.
   - Validar entradas con `INotifyDataErrorInfo` (`ValidatableViewModel`) y mostrar errores de negocio (stock insuficiente, bodega origen y destino iguales) en pantalla sin excepciones crudas.
4. **Tarea 3 — Vista de Stock por Bodega y Alerta de Stock Bajo:**
   - Mostrar existencias por bodega (`GetStockByWarehouse`) y stock total por producto (`GetTotalStockByProduct`), resaltando visualmente los productos cuyo stock esté en o por debajo de su umbral mínimo.
5. **Tarea 4 — Historial de Movimientos con Filtros:**
   - Ampliar `MovementsView` / `MovementsViewModel` con filtros por rango de fechas, tipo de movimiento (`Entry`, `Exit`, `Transfer`) y bodega, mostrando nombres legibles de producto y bodega además de los IDs.
6. **Tarea 5 — Pruebas xUnit y Criterio de Aceptación:**
   - Agregar tests para el flujo completo del MVP 3 (entrada de 50 unidades, salida de 10 y traslado de 20 dejando los saldos correctos por bodega, rechazo de traslado mayor al stock disponible, filtros de historial y alertas de stock bajo).
   - Probar manualmente de punta a punta en la app WPF y actualizar este `README.md` al cerrar el MVP 3.

### Convenciones y reglas activas del proyecto

- **Arquitectura:** Clean Architecture + MVVM estricta. Las vistas XAML solo enlazan ViewModels (`code-behind` sin lógica de negocio); los ViewModels consumen interfaces de `Application`/`Domain` e `IDialogService`, nunca repositorios concretos ni `JsonDataContext`.
- **Idioma y documentación:** Identificadores de código en inglés con comentarios XML (`/// <summary>`) en clases, constructores y métodos; documentación, títulos y mensajes de interfaz en español; mantener las claves `[JsonPropertyName]` existentes para compatibilidad con `data/*.json`.
- **Git:** Ramas `feature/...`, `fix/...`, `docs/...`; commits con formato `tipo(scope): descripción`.
- **Notas de compilación/tests en entorno local:** La solución usa `net10.0` (`Domain`, `Application`, `Infrastructure`) y `net10.0-windows` con WPF (`UI`, `Tests`). En archivos del proyecto WPF/Tests, incluir explícitamente `using System.IO;` cuando se usen `Path`, `File` o `Directory`.
- **Formato de seguimiento en el chat:** Al completar cada bloque, reportar qué se encontró/cambió, dejar el conteo de tareas completadas vs. faltantes y una breve explicación técnica de por qué ocurría cada problema.
