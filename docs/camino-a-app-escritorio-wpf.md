# Camino a una app de escritorio con WPF

**Estado: pendiente.** La solución actual tiene una UI de consola prevista y persistencia JSON. Esta guía describe los pasos para convertirla en una aplicación de escritorio WPF con datos locales. No implica que WPF, formularios ni persistencia local adicional ya estén implementados.

## 1. Acordar alcance y plataforma

- Confirmar que la aplicación se ejecutará en Windows y elegir una versión de .NET compatible con WPF.
- Definir las pantallas iniciales: productos, bodegas, inventario, entradas, salidas y traslados.
- Acordar qué usuarios usarán la aplicación y dónde residirán los datos locales: por usuario de Windows o compartidos en el equipo.

## 2. Crear el proyecto WPF

- Añadir un proyecto de presentación WPF a la solución y configurarlo con el framework objetivo elegido.
- Mantener el dominio y la lógica de aplicación fuera de las vistas; la UI debe consumir servicios de `TiendaBodegas.Application`.
- Configurar el arranque de la ventana principal y la inyección de dependencias desde el proyecto de presentación.
- Organizar pantallas y controles con MVVM (Model-View-ViewModel), enlazando propiedades y comandos en vez de poner reglas de negocio en el code-behind.

## 3. Completar los servicios y repositorios

- Terminar las interfaces y sus implementaciones para productos, bodegas y movimientos.
- Añadir operaciones para listar, crear, editar y buscar productos y bodegas.
- Centralizar validaciones en aplicación/dominio: campos requeridos, SKU único, precio no negativo, cantidades positivas, referencias existentes y stock suficiente.
- Hacer que formularios y comandos llamen a servicios; no permitir que escriban directamente en archivos o tablas.

## 4. Elegir persistencia local

- Comparar JSON con SQLite. Para uso local con consultas, relaciones y transacciones, evaluar SQLite como primera opción; conservar JSON solo si el volumen y las consultas son muy sencillos.
- Guardar el archivo de base de datos en una carpeta de datos de usuario (por ejemplo, dentro de `LocalApplicationData`), no junto al ejecutable, que puede estar en una ubicación sin permisos de escritura.
- Mantener secretos fuera del repositorio. Si no hay credenciales remotas, no introducir una cadena de conexión con contraseña.
- Versionar el esquema con migraciones o scripts y documentar cómo crear una copia de seguridad y restaurarla.
- Preparar una importación desde los JSON existentes, validar conteos y saldos y conservar una copia de respaldo antes de cambiar la persistencia activa.

## 5. Construir formularios

### Productos

- Mostrar una tabla con SKU, nombre y precio, junto con búsqueda por SKU/nombre.
- Añadir un formulario con SKU, nombre y precio; marcar requeridos, validar SKU duplicado y precio válido y presentar errores junto a cada campo.
- Ofrecer acciones claras para guardar, cancelar y editar. Confirmar antes de eliminar si se incorpora eliminación.

### Bodegas

- Crear una lista con nombre y dirección y formularios para alta y edición.
- Validar campos requeridos y mostrar mensajes comprensibles si la bodega no se puede guardar.

### Inventario y movimientos

- Mostrar existencias por producto y bodega.
- Usar formularios distintos para entrada, salida y traslado, con selectores de producto y bodega y una cantidad mayor que cero.
- En traslados, pedir bodega de origen y destino distintas; mostrar el stock disponible antes de confirmar.
- Mostrar confirmación y resultado del guardado, y conservar un historial consultable de movimientos.

## 6. Usabilidad y errores

- Mantener navegación consistente, etiquetas visibles, foco inicial y orden de tabulación razonable.
- Deshabilitar acciones durante guardados para evitar envíos repetidos.
- Mostrar errores de validación en contexto y reservar alertas para fallos que requieran atención.
- Manejar errores de archivo o base de datos con mensajes útiles; no mostrar trazas técnicas al usuario final.

## 7. Verificación y entrega

- Probar servicios, validaciones y repositorios con pruebas automatizadas.
- Verificar los flujos de alta/edición de productos, entradas, salidas y traslados con datos locales.
- Probar cierre y reinicio para confirmar persistencia, además de copia y restauración de datos.
- Publicar una compilación Windows y documentar instalación, ubicación de datos, actualización y respaldo.

## Orden sugerido

1. Completar dominio, servicios y repositorios.
2. Decidir persistencia local y preparar migración desde JSON.
3. Añadir WPF, MVVM y el formulario de productos.
4. Añadir bodegas, inventario y formularios de movimientos.
5. Verificar persistencia, validaciones, errores y publicación.
