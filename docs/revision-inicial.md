# Revisión inicial y seguimiento

## Resumen
- Revisión estática inicial y seguimiento de los dos hallazgos bajos solicitados.
- Se encontraron riesgos de compilación, pérdida de datos al cargar JSON y movimientos de inventario sin validaciones suficientes.
- No encontré secretos ni dependencias NuGet externas declaradas en los proyectos revisados.
- No ejecuté compilación ni pruebas; las advertencias del compilador quedan sin verificar.

## Errores y posibles bugs

- [ ] **[Severidad: alta]** La aplicación ejecutable no tiene punto de entrada
  - Archivo: `src/TiendaBodegas.UI/Program.cs`
  - Qué pasa: El archivo está vacío aunque el proyecto UI declara `OutputType` como `Exe`; no se ve un método `Main` ni instrucciones de nivel superior. Esto normalmente impide compilar el ejecutable (CS5001).
  - Sugerencia: Agregar el punto de entrada de la aplicación y conectar allí el inicio de la interfaz.

- [ ] **[Severidad: alta]** La deserialización JSON puede restablecer propiedades a sus valores por defecto
  - Archivo: `src/TiendaBodegas.Infrastructure/Persistence/Json/JsonDataContext.cs:38`
  - Qué pasa: Las propiedades de las entidades usan setters privados y la configuración de `System.Text.Json` no opta por esos setters; al cargar los archivos con constructores vacíos, campos como IDs, nombres, cantidades y fechas pueden quedar en valores predeterminados. La documentación de .NET indica que los setters privados requieren inclusión explícita para deserializarse.
  - Sugerencia: Definir una estrategia de deserialización compatible con la encapsulación de las entidades y probar una ida y vuelta de datos.

- [ ] **[Severidad: alta]** Los movimientos aceptan cantidades no positivas
  - Archivo: `src/TiendaBodegas.Application/Services/InventoryManager.cs:17`
  - Qué pasa: Ningún método valida que `quantity` sea mayor que cero; cantidades negativas pueden invertir el efecto del movimiento y aumentar inventario, y cero registra operaciones vacías.
  - Sugerencia: Rechazar cantidades menores o iguales a cero antes de registrar entradas, salidas o traslados.

- [ ] **[Severidad: alta]** Las salidas no comprueban el stock disponible
  - Archivo: `src/TiendaBodegas.Application/Services/InventoryManager.cs:54`
  - Qué pasa: `RegisterExit` registra el movimiento directamente sin consultar el stock de la bodega; el sistema puede dejar inventario negativo.
  - Sugerencia: Calcular y validar el saldo disponible de la bodega antes de guardar una salida.

- [ ] **[Severidad: media]** La escritura de los tres archivos JSON no es atómica
  - Archivo: `src/TiendaBodegas.Infrastructure/Persistence/Json/JsonDataContext.cs:22`
  - Qué pasa: Los archivos se sobrescriben uno tras otro; un fallo de E/S a mitad del guardado puede dejar datos de bodegas, productos y tickets en versiones distintas.
  - Sugerencia: Guardar mediante archivos temporales y reemplazo coordinado, o adoptar una persistencia con transacciones.

## Seguridad y datos sensibles

No encontré contraseñas, tokens ni cadenas de conexión reales en los archivos fuente y configuración revisados. Esta observación cubre únicamente los archivos visibles del repositorio y no los archivos ignorados o generados.

## Estructura y funcionalidades incompletas

- [ ] **[Severidad: alta]** No hay implementación del repositorio de tickets
  - Archivo: `src/TiendaBodegas.Infrastructure/Persistence/Repositories/StockMovementRepository.cs`
  - Qué pasa: El archivo está vacío, pero `InventoryManager` depende de `IStockMovementRepository` para consultar y guardar los movimientos. No se encuentra otra implementación en el árbol revisado.
  - Sugerencia: Implementar el repositorio de tickets y su persistencia antes de usar las operaciones de inventario.

- [ ] **[Severidad: media]** No se valida que los IDs de producto y bodega existan en todos los movimientos
  - Archivo: `src/TiendaBodegas.Application/Services/InventoryManager.cs:45`
  - Qué pasa: Entradas y salidas aceptan IDs sin comprobar referencias; el traslado valida las bodegas, pero no que exista el producto. Pueden quedar movimientos huérfanos.
  - Sugerencia: Validar las referencias de producto y bodega antes de aceptar cada movimiento.

- [ ] **[Severidad: media]** Componentes de despacho, configuración y DTOs están vacíos
  - Archivo: `src/TiendaBodegas.Application/Services/DispatchManager.cs`
  - Qué pasa: También están vacíos `src/TiendaBodegas.Application/DTOs/StockMovementRequest.cs`, `src/TiendaBodegas.Application/DTOs/ShipmentPlanDto.cs`, `src/TiendaBodegas.UI/DependencyInjection.cs` y `src/TiendaBodegas.Infrastructure/Services/PrinterService.cs`; esas piezas no tienen implementación visible.
  - Sugerencia: Completar o retirar formalmente los componentes que formen parte del alcance funcional esperado.

- [ ] **[Severidad: media]** No hay proyecto ni archivos de pruebas visibles
  - Archivo: `TiendaBodegas.slnx`
  - Qué pasa: La solución contiene cuatro proyectos de producción y no se encontraron pruebas automatizadas para movimientos, persistencia o reglas de stock.
  - Sugerencia: Incorporar pruebas para los flujos de inventario y la carga/guardado JSON.

## Convenciones y documentación

- [ ] **[Severidad: media]** La rama y los commits visibles no siguen las convenciones indicadas
  - Archivo: historial Git (rama actual `chore/revision-agente`)
  - Qué pasa: La rama usa el prefijo `chore/` en vez de `feature/`, `fix/` o `docs/`; los últimos asuntos (`Feat: ...`) tampoco usan `tipo(scope): descripción`.
  - Sugerencia: Aplicar los prefijos y el formato acordados en futuras ramas y commits.

- [x] **[Severidad: baja, resuelto]** El idioma de los identificadores de código no coincidía con la convención
  - Archivo: `src/TiendaBodegas.Domain/Entities/Warehouse.cs`
  - Qué pasa: Los identificadores y nombres de archivo del código fueron pasados a inglés; se conservaron los nombres históricos de las claves JSON para no cambiar su formato.
  - Sugerencia: Mantener la convención de inglés en código y español en documentación.

- [x] **[Severidad: baja, resuelto]** El README conservaba una lista de pendientes desactualizada
  - Archivo: `README.md:24`
  - Qué pasa: El README fue sustituido por una descripción del estado actual y enlaza la ruta SQL documentada.
  - Sugerencia: Mantener actualizado el estado de implementación al completar trabajo futuro.

## Advertencias y dependencias

No se encontraron referencias `PackageReference` a paquetes NuGet externos en los proyectos revisados. No puedo confirmar advertencias de compilación ni evaluar avisos de seguridad de dependencias sin restaurar/compilar o inspeccionar un inventario de dependencias; no ejecuté esos comandos para evitar generar archivos. El objetivo declarado es `net10.0` en los cuatro proyectos.

## Lo que NO revisé

- No compilé ni ejecuté pruebas; por tanto, los errores o warnings reales del compilador no están confirmados.
- No analicé `bin/`, `obj/`, `node_modules/` ni archivos generados.
- No inspeccioné historial completo ni ramas remotas; las convenciones Git se evaluaron con la rama actual y los dos asuntos de commit locales visibles.
- No evalué comportamiento en runtime, datos JSON externos ni el entorno de despliegue.

**Pendientes fuera del límite de 30:** 0.
