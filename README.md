# ShortcutTasker

Aplicación de Windows para crear atajos que abren aplicaciones, webs y carpetas; escriben texto; ejecutan comandos; controlan multimedia; y envían combinaciones de teclas a una aplicación concreta.

## Instalar

Descarga `ShortcutTasker-Setup.exe` desde [Releases](https://github.com/IJustDaniii/shortcuttasker/releases). El instalador instala la aplicación para el usuario actual, crea un acceso en Inicio y la inicia automáticamente con Windows. La configuración se guarda en `%APPDATA%\AtajosLibres\atajos.xml` para conservar los atajos de la versión anterior.

**Requisitos:** Windows 10 u 11 y .NET Framework 4.8. El ejecutable y el instalador de esta versión no tienen firma de código. En equipos con Control inteligente de aplicaciones o una política que exija firma, Windows puede bloquearlos; publicar el archivo en GitHub no elimina ese bloqueo. No desactives la protección del sistema solo para instalarlos.

## Acciones de aplicaciones

En **Nuevo atajo**, elige **Aplicación, archivo o carpeta** y pulsa **Instaladas…**. Debajo de la aplicación elegida aparece **Qué hacer con esta aplicación**:

- **Discord:** abrir o mostrar la app; silenciar/activar el micrófono; ensordecer/volver a oír. Los dos controles de voz usan los [atajos oficiales de Discord](https://support.discord.com/hc/en-us/articles/225977308--Windows-Discord-Hotkeys).
- **Spotify:** abrir o mostrar la app; reproducir/pausar; siguiente canción; canción anterior. Se usan los botones accesibles del reproductor y, si no están disponibles, los atajos de teclado de la app.
- **Otras apps:** abrir o mostrar; enviar una combinación de teclas personalizada.

El catálogo procede de la carpeta de aplicaciones de Windows, por lo que [algunas apps pueden faltar](https://learn.microsoft.com/en-us/windows/configuration/store/find-aumid). Para abrir una app ya seleccionada, ShortcutTasker detecta su proceso automáticamente. La opción **Enviar combinación de teclas** permite seleccionar una ventana abierta con **Ventana…** si no se detecta el proceso. La antigua acción de volumen individual de Discord no aparece al crear atajos; los atajos existentes se conservan, aunque ya no se pueden editar desde esta versión.

## Actualizaciones

Al iniciarse, ShortcutTasker consulta la última versión publicada en GitHub. Si hay una versión superior con instalador, ofrece instalarla. Al elegir «No», volverá a ofrecerla en el próximo inicio. También puedes pulsar **Buscar actualizaciones**. La descarga se comprueba con el SHA-256 publicado por GitHub antes de ejecutarse. Los cambios de código sin una nueva versión publicada no activan el aviso, porque no tienen instalador que aplicar.

## Compilar

Se necesita el compilador de .NET Framework de Windows e [Inno Setup 6](https://jrsoftware.org/isinfo.php). Ejecuta `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1` desde la carpeta del proyecto. El programa queda en `build\ShortcutTasker.exe` y el instalador en `dist\ShortcutTasker-Setup.exe`.

Para ejecutar las pruebas de lógica y activación de ventanas: `powershell -NoProfile -ExecutionPolicy Bypass -File .\test.ps1`.

El proyecto no almacena contraseñas ni tokens de GitHub. La comprobación de actualizaciones usa la API pública de Releases.
