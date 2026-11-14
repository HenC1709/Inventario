# Camino a persistencia SQL

**Estado: pendiente.** La aplicación actual persiste datos en JSON. Este documento marca una ruta de trabajo futura; no se ha elegido proveedor SQL ni se ha implementado una migración.

## Etapas propuestas

1. **Elegir proveedor y alcance.** Decidir entre SQLite para una instalación local sencilla o un servidor SQL si se requiere acceso concurrente desde varios procesos o equipos. Registrar también versión, entorno y estrategia de copias de seguridad.
2. **Definir el modelo relacional.** Crear tablas para `Warehouses`, `Products`, `StockMovements` y, si se implementa despacho, `Shipments`. Definir claves primarias, claves foráneas, índices y reglas de nulabilidad; preservar el significado actual de los movimientos de entrada, salida y traslado.
3. **Separar persistencia del dominio.** Completar las interfaces de repositorio y hacer que los servicios de aplicación dependan de ellas, sin acoplarlos al proveedor SQL.
4. **Implementar el adaptador SQL.** Añadir el proveedor y configuración mediante dependencias del proyecto de infraestructura. Mantener credenciales fuera del código fuente y usar configuración segura por entorno si el proveedor requiere secretos.
5. **Crear esquema versionado.** Añadir migraciones o scripts versionados para crear y evolucionar el esquema. Aplicarlos de forma explícita en entornos controlados, con copia previa y procedimiento de reversión.
6. **Migrar datos JSON.** Escribir una herramienta repetible que lea los archivos actuales, valide referencias e IDs y cargue los datos dentro de una transacción. Conservar los JSON como respaldo hasta verificar conteos y saldos.
7. **Verificar y cambiar el arranque.** Probar altas, consultas, movimientos y reinicio de la aplicación contra SQL; comparar existencias con los datos de origen y documentar configuración, restauración y operación.

## Criterios antes de retirar JSON

- Conteos de bodegas, productos y movimientos conciliados.
- Saldos por producto y bodega coincidentes antes y después de la migración.
- Restricciones de integridad y transacciones verificadas.
- Copia de seguridad y restauración probadas.
- Proveedor, cadena de conexión y despliegue documentados para cada entorno.
