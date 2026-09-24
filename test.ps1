$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root
New-Item -ItemType Directory -Force -Path 'build' | Out-Null
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
$sources = Get-ChildItem 'src' -Filter '*.cs' | Select-Object -ExpandProperty FullName
& $compiler /nologo /target:exe /codepage:65001 /main:CoreTests /out:build\CoreTests.exe $refs $sources tests\CoreTests.cs
if ($LASTEXITCODE -ne 0) { throw 'No se compilaron las pruebas de lógica.' }
& .\build\CoreTests.exe
if ($LASTEXITCODE -ne 0) { throw 'Fallaron las pruebas de lógica.' }
& $compiler /nologo /target:winexe /codepage:65001 /out:build\WindowHost.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll tests\WindowHost.cs
if ($LASTEXITCODE -ne 0) { throw 'No se compiló la ventana de prueba.' }
& $compiler /nologo /target:exe /codepage:65001 /main:FocusTests /out:build\FocusTests.exe $refs $sources tests\FocusTests.cs
if ($LASTEXITCODE -ne 0) { throw 'No se compilaron las pruebas de ventanas.' }
& .\build\FocusTests.exe
if ($LASTEXITCODE -ne 0) { throw 'Fallaron las pruebas de ventanas.' }
