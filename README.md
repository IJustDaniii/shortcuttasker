# ShortcutTasker

Aplicación de Windows para crear atajos que abren aplicaciones, webs y carpetas; escriben texto; ejecutan comandos; controlan multimedia; y envían combinaciones de teclas a una aplicación concreta.

## Instalar

Descarga `ShortcutTasker-Setup.exe` desde [Releases](https://github.com/IJustDaniii/shortcuttasker/releases). El instalador instala la aplicación para el usuario actual, crea un acceso en Inicio y la inicia automáticamente con Windows. La configuración se guarda en `%APPDATA%\AtajosLibres\atajos.xml` para conservar los atajos de la versión anterior.

**Requisitos:** Windows 10 u 11 y .NET Framework 4.8. El ejecutable y el instalador de esta versión no tienen firma de código. En equipos con Control inteligente de aplicaciones o una política que exija firma, Windows puede bloquearlos; publicar el archivo en GitHub no elimina ese bloqueo. No desactives la protección del sistema solo para instalarlos.

## Acciones nuevas en 1.0.0

- **Abrir aplicación:** si ya existe una ventana del mismo ejecutable, la restaura y la activa. Para accesos directos o lanzadores especiales, indica el nombre del proceso en el campo opcional. Para apps empaquetadas `shell:AppsFolder`, Windows gestiona su activación.
- **Enviar atajo a una aplicación:** selecciona el proceso y la combinación que recibirá esa ventana. La aplicación debe estar abierta y Windows debe permitir activarla.
- **Discord, micrófono:** alterna silencio con `Ctrl+Mayús+M`.
- **Discord, audio:** alterna ensordecimiento con `Ctrl+Mayús+D`.
- **Discord, persona:** indica el nombre visible exacto de una persona en la llamada. ShortcutTasker intenta localizar su control de volumen mediante la accesibilidad de Windows y alternar entre cero y el volumen anterior. Si encuentra varias coincidencias o Discord no expone el control, muestra un error y no cambia ningún volumen. Esta acción depende de la interfaz actual de Discord.

Los controles de Discord usan sus [atajos oficiales para Windows](https://support.discord.com/hc/en-us/articles/225977308--Windows-Discord-Hotkeys). Si personalizas esos atajos en Discord, usa la acción genérica con las teclas nuevas. El volumen individual de participantes se ajusta en el menú de cada persona, como indica la [ayuda de Discord](https://support.discord.com/hc/en-us/articles/205287897-How-do-I-adjust-the-volume-level-of-individual-users-in-my-server).

## Actualizaciones

Al iniciarse, ShortcutTasker consulta la última versión publicada en GitHub. Si hay una versión superior con instalador, ofrece instalarla. Al elegir «No», volverá a ofrecerla en el próximo inicio. También puedes pulsar **Buscar actualizaciones**. La descarga se comprueba con el SHA-256 publicado por GitHub antes de ejecutarse. Los cambios de código sin una nueva versión publicada no activan el aviso, porque no tienen instalador que aplicar.

## Compilar

Se necesita el compilador de .NET Framework de Windows e [Inno Setup 6](https://jrsoftware.org/isinfo.php). Ejecuta `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1` desde la carpeta del proyecto. El programa queda en `build\ShortcutTasker.exe` y el instalador en `dist\ShortcutTasker-Setup.exe`.

Para ejecutar las pruebas de lógica y activación de ventanas: `powershell -NoProfile -ExecutionPolicy Bypass -File .\test.ps1`.

El proyecto no almacena contraseñas ni tokens de GitHub. La comprobación de actualizaciones usa la API pública de Releases.
