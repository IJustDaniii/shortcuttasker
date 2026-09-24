$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$gac = Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL'
$winrt = & (Join-Path $root 'restore-winrt.ps1')
$refs = @(
    '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', '/r:System.Web.Extensions.dll', '/r:Microsoft.CSharp.dll',
    ('/r:' + (Join-Path $gac 'UIAutomationClient\v4.0_4.0.0.0__31bf3856ad364e35\UIAutomationClient.dll')),
    ('/r:' + (Join-Path $gac 'UIAutomationTypes\v4.0_4.0.0.0__31bf3856ad364e35\UIAutomationTypes.dll')),
    ('/r:' + (Join-Path $gac 'WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll')),
    ('/r:' + (Join-Path $gac 'System.Runtime\v4.0_4.0.0.0__b03f5f7f11d50a3a\System.Runtime.dll')),
    ('/r:' + (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\System.Runtime.WindowsRuntime.dll')),
    ('/r:' + (Join-Path $winrt 'Windows.WinMD')),
    ('/r:' + (Join-Path $winrt 'Windows.Foundation.FoundationContract.winmd')),
    ('/r:' + (Join-Path $winrt 'Windows.Foundation.UniversalApiContract.winmd'))
)
New-Item -ItemType Directory -Force -Path (Join-Path $root 'build'), (Join-Path $root 'dist') | Out-Null
$sources = Get-ChildItem (Join-Path $root 'src') -Filter '*.cs' | Select-Object -ExpandProperty FullName
& $compiler /nologo /target:winexe /platform:anycpu /codepage:65001 ('/out:' + (Join-Path $root 'build\ShortcutTasker.exe')) ('/win32icon:' + (Join-Path $root 'assets\icon.ico')) $refs $sources
if ($LASTEXITCODE -ne 0) { throw 'No se pudo compilar ShortcutTasker.' }
$iscc = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
if (!(Test-Path $iscc)) { $iscc = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' }
if (!(Test-Path $iscc)) { throw 'Instala Inno Setup 6 para crear el instalador.' }
& $iscc (Join-Path $root 'installer\ShortcutTasker.iss')
if ($LASTEXITCODE -ne 0) { throw 'No se pudo crear el instalador.' }
