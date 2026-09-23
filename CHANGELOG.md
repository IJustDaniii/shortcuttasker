# Cambios

## 1.1.3

- Aclara el formulario vacío: primero hay que seleccionar una app instalada para ver sus acciones.
- Distingue el texto de apertura de app del aviso de acción inmediata de Discord y Spotify.

## 1.1.2

- Los controles de voz de Discord y reproducción de Spotify se ejecutan al pulsar la última tecla, sin esperar a soltar la combinación.
- Discord y Spotify reciben la acción por sus controles accesibles en segundo plano; ShortcutTasker no abre ni activa sus ventanas. La app debe estar abierta y mostrar esos controles.
- Los atajos que abren aplicaciones conservan su ejecución al soltar las teclas modificadoras.

## 1.1.1

- Al elegir una aplicación instalada, muestra sus acciones directamente debajo: Discord ofrece silenciar/activar el micrófono y ensordecer/volver a oír; Spotify ofrece reproducir/pausar, siguiente canción y canción anterior.
- Las demás aplicaciones permiten abrir o mostrar su ventana y enviar una combinación de teclas personalizada.
- El campo «Nombre del proceso si ya está abierto» desaparece de la acción de abrir. ShortcutTasker usa el proceso detectado en el catálogo o la ruta del ejecutable.
- Las acciones antiguas de volumen individual en Discord dejan de ofrecerse al crear atajos; las reglas existentes se conservan.

## 1.1.0

- Selector con búsqueda de aplicaciones registradas en el menú Inicio de Windows.
- Detección del ejecutable de apps de escritorio y del proceso de apps empaquetadas cuando Windows lo ofrece.
- Selector de ventanas abiertas para dirigir atajos a una app cuyo proceso no se detecte automáticamente.
- La acción de enviar teclas puede abrir la app elegida y esperar a que aparezca su ventana.

## 1.0.0

- Instalador de usuario con acceso en Inicio, inicio automático y desinstalación.
- Búsqueda de nuevas versiones en GitHub y actualización con comprobación SHA-256.
- Activación de la ventana de una aplicación que ya esté abierta.
- Combinaciones de teclas dirigidas a aplicaciones y controles de voz de Discord.
- Control del volumen individual de un participante de Discord cuando su interfaz lo expone a la accesibilidad de Windows.
