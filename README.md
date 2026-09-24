# ShortcutTasker

Aplicación de Windows para crear atajos que abren aplicaciones, webs y carpetas; escriben texto; ejecutan comandos; controlan multimedia; y envían combinaciones de teclas a una aplicación concreta.

## Instalar

Descarga `ShortcutTasker-Setup.exe` desde [Releases](https://github.com/IJustDaniii/shortcuttasker/releases). El instalador instala la aplicación para el usuario actual, crea un acceso en Inicio y la inicia automáticamente con Windows. La configuración se guarda en `%APPDATA%\AtajosLibres\atajos.xml` para conservar los atajos de la versión anterior.

**Requisitos:** Windows 10 versión 1809 o posterior, o Windows 11, y .NET Framework 4.8. El ejecutable y el instalador de esta versión no tienen firma de código. En equipos con Control inteligente de aplicaciones o una política que exija firma, Windows puede bloquearlos; publicar el archivo en GitHub no elimina ese bloqueo. No desactives la protección del sistema solo para instalarlos.

## Elegir un atajo

En **Nuevo atajo**, pulsa **Capturar** y después la tecla o el control del ratón que quieras usar. Puedes mantener pulsados Win, Ctrl, Alt o Mayús durante la captura para marcar los modificadores automáticamente, o marcarlos en el formulario. Se requiere al menos uno para conservar el uso normal de teclas y clics sin modificador. El botón **Cancelar** de la captura permite volver a elegir.

Se admiten las teclas que Windows entrega como códigos virtuales, clic izquierdo, derecho y central, los dos botones laterales estándar y la rueda vertical u horizontal en ambas direcciones. La tecla Fn de algunos teclados se procesa en el hardware y puede no generar un código de Windows. Los controles del ratón se ejecutan al pulsar o mover la rueda; se evita que el clic o desplazamiento asociado llegue a la ventana activa cuando coincide con un atajo.

## Acciones de aplicaciones

En **Nuevo atajo**, elige **Aplicación, archivo o carpeta** y pulsa **Instaladas…**. Debajo de la aplicación elegida aparece **Qué hacer con esta aplicación**:

- **Discord:** abrir o mostrar la app; silenciar/activar el micrófono; ensordecer/volver a oír.
- **Spotify:** abrir o mostrar la app; reproducir/pausar; siguiente canción; canción anterior.
- **Otras apps:** abrir o mostrar; usar un atajo propio de la aplicación.

El catálogo procede de la carpeta de aplicaciones de Windows, por lo que [algunas apps pueden faltar](https://learn.microsoft.com/en-us/windows/configuration/store/find-aumid). Para abrir una app ya seleccionada, ShortcutTasker detecta su proceso automáticamente. **Usar un atajo propio de la aplicación** significa enviar a esa app las teclas de una función que ya tenga configurada. Esta opción muestra su ventana para entregarle las teclas; **Ventana…** permite elegirla si no se detecta el proceso. La antigua acción de volumen individual de Discord no aparece al crear atajos; los atajos existentes se conservan, aunque ya no se pueden editar desde esta versión.

Todos los atajos se activan al pulsar la última tecla. Los controles de voz de Discord y de reproducción de Spotify no traen su ventana al frente. Discord debe estar abierto y exponer sus controles de voz a la accesibilidad de Windows; Spotify debe tener una sesión multimedia activa. Estos controles no inician la app si está cerrada.

## Actualizaciones

Al iniciarse, ShortcutTasker consulta la última versión publicada en GitHub. Si hay una versión superior con instalador, ofrece instalarla. Al elegir «No», volverá a ofrecerla en el próximo inicio. También puedes pulsar **Buscar actualizaciones**. La descarga se comprueba con el SHA-256 publicado por GitHub antes de ejecutarse. Los cambios de código sin una nueva versión publicada no activan el aviso, porque no tienen instalador que aplicar.

## Compilar

Se necesita el compilador de .NET Framework de Windows e [Inno Setup 6](https://jrsoftware.org/isinfo.php). La compilación descarga de NuGet una versión fijada de `Microsoft.Windows.SDK.Contracts` y verifica su SHA-256 para usar la API multimedia de Windows; esta dependencia de compilación no se incluye en el instalador. Ejecuta `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1` desde la carpeta del proyecto. El programa queda en `build\ShortcutTasker.exe` y el instalador en `dist\ShortcutTasker-Setup.exe`.

Para ejecutar las pruebas de lógica, ratón y activación de ventanas: `powershell -NoProfile -ExecutionPolicy Bypass -File .\test.ps1`.

El proyecto no almacena contraseñas ni tokens de GitHub. La comprobación de actualizaciones usa la API pública de Releases.
