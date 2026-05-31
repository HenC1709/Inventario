# TiendaBodegas — Sesión 2026-05-30

## ✅ Lo que se hizo hoy

### 🐛 Bug fixes

#### `TipoMovimiento.cs`
- `traslado` → `Traslado` (PascalCase). Causaba error de compilación en `InventarioManager`.

#### `Ticket.cs`
- `BodegDestinoId` → `BodegaDestinoId` (faltaba la `a`)
- Parámetro `bodegaId` → `destinoId` en el constructor (nombre ambiguo)

#### `InventarioManager.cs`
- `RealizarTraslado`: se calculaba `stockOrigen` pero nunca se validaba contra `cantidad` — el traslado se ejecutaba aunque no hubiera stock suficiente
- `ObtenerStockPorBodega`: los traslados siempre restaban del stock, sin importar si la bodega era origen o destino
- `ObtenerStockPorTotalProducto`: `trasladosOrigen` y `trasladosDestino` siempre se cancelaban entre sí — se simplificó a `entradas - salidas`
- Typo: `_tickerRepository` → `_ticketRepository`

---

### 🏗️ Archivos construidos desde cero

#### `Bodega.cs` y `Producto.cs`
- Agregados constructores con parámetros (las entidades no se podían instanciar)
- Agregado constructor vacío para deserialización JSON
- Pendiente: agregar `AsignarId(int id)` a ambas (y a `Ticket.cs`)

#### `JsonDbContext.cs`
- Manejo de archivos JSON como capa de persistencia
- `GuardarJson()`: serializa las 3 listas a `data/bodegas.json`, `productos.json`, `tickets.json`
- `ObtenerJson()`: deserializa con `File.Exists()` para no explotar en el primer arranque
- La carpeta `data/` se crea automáticamente en runtime junto al ejecutable

#### `BodegaRepository.cs`
- Implementa `IBodegaRepository` + `IRepository<Bodega>`
- Métodos: `GetAll`, `GetById`, `GetByName`, `GetByAddress`, `Add`, `Update`
- `Add` genera IDs con `Max + 1` y los asigna vía `AsignarId()`
- `Update` usa patrón Remove + Add sobre la lista en memoria
- Persiste automáticamente con `GuardarJson()` después de cada escritura

---

## ⏳ Pendiente para la próxima sesión

- [ ] Agregar `AsignarId(int id)` a `Bodega.cs`, `Producto.cs` y `Ticket.cs`
- [ ] `TicketRepository.cs`
- [ ] `DespachoManager.cs`
- [ ] `MovimientoStockRequest.cs` y `PlanificacionEnvioDto.cs`
- [ ] `DependencyInjection.cs`
- [ ] `Program.cs` (menú de consola)