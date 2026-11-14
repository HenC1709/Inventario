# TiendaBodegas

Aplicación .NET para registrar movimientos de inventario entre bodegas y consultar existencias por producto.

## Estructura

- `src/TiendaBodegas.Domain`: entidades, enumeraciones e interfaces de repositorio.
- `src/TiendaBodegas.Application`: servicios de aplicación y DTOs.
- `src/TiendaBodegas.Infrastructure`: persistencia JSON y adaptadores de infraestructura.
- `src/TiendaBodegas.UI`: punto de entrada previsto para la interfaz de consola.

El código usa identificadores en inglés; la documentación del proyecto está en español.

## Estado actual

- `InventoryManager` define entradas, salidas, traslados y consultas de stock.
- `JsonDataContext` guarda bodegas, productos y movimientos en archivos JSON bajo `data/` junto al ejecutable. Las claves JSON existentes se mantienen para compatibilidad con los datos guardados.
- La persistencia SQL está pendiente; la ruta propuesta está en [docs/camino-a-sql.md](docs/camino-a-sql.md).
- La implementación de `IStockMovementRepository`, el registro de dependencias y el punto de entrada de la UI siguen pendientes.
- Los DTOs de movimientos y planificación y los componentes de despacho e impresión están sin implementar.
- No hay un proyecto de pruebas automatizadas en la solución todavía.

## Compilación

La solución está definida en `TiendaBodegas.slnx` y los proyectos apuntan a `net10.0`. La compilación no se ha verificado en esta revisión.
