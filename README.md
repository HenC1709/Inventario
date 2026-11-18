# TiendaBodegas

Aplicación de escritorio Windows (.NET / WPF) para gestionar bodegas, productos, movimientos de inventario y existencias por bodega con Clean Architecture y MVVM.

## Estructura

- `src/TiendaBodegas.Domain`: entidades, enumeraciones e interfaces de repositorio.
- `src/TiendaBodegas.Application`: servicios de aplicación, validaciones de negocio e interfaces.
- `src/TiendaBodegas.Infrastructure`: persistencia JSON (`JsonDataContext`), repositorios e infraestructura.
- `src/TiendaBodegas.UI`: aplicación de escritorio WPF (`net10.0-windows`) con MVVM (`CommunityToolkit.Mvvm`) e inyección de dependencias (`Microsoft.Extensions.DependencyInjection`).
- `tests/TiendaBodegas.Tests`: proyecto de pruebas automatizadas con `xUnit`.

El código usa identificadores en inglés; la documentación del proyecto está en español.

## Estado actual (MVP 1 — Base WPF completado)

- **Dominio y persistencia JSON:** las entidades (`Warehouse`, `Product`, `StockMovement`, `Shipment`) cuentan con `[JsonInclude]` y `AssignId`; `JsonDataContext` guarda bodegas, productos y movimientos de forma atómica bajo `data/` junto al ejecutable conservando las claves JSON existentes.
- **Repositorios y reglas de negocio:** `WarehouseRepository`, `ProductRepository` y `StockMovementRepository` implementados; `InventoryManager` valida cantidades mayores a cero, existencia de producto y bodega, y stock disponible en salidas y traslados.
- **Interfaz WPF (MVP 1):** `App.xaml.cs` y `DependencyInjection.cs` cargan los JSON al iniciar, siembran datos de prueba cuando la carpeta está vacía y guardan al cerrar. `MainWindow` ofrece navegación lateral (Bodegas, Productos y Movimientos) con vistas `DataGrid` de solo lectura.
- **Pruebas automatizadas:** `TiendaBodegas.Tests` cubre la ida y vuelta de `JsonDataContext`, las reglas de `InventoryManager`, el seeding inicial y la navegación de `MainViewModel` (`11/11` pruebas pasando).
- **Pendiente para próximos MVPs:**
  - **MVP 2:** formularios CRUD (alta, edición, borrado con confirmación y búsqueda) usando `INotifyDataErrorInfo`.
  - **MVP 3:** pantallas de entradas, salidas y traslados, consulta de stock por bodega y filtros en historial.
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
