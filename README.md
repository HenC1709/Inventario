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
