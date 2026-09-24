$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$package = Join-Path $root 'build\microsoft.windows.sdk.contracts.10.0.19041.2.nupkg'
$extract = Join-Path $root 'build\winrt-contracts'
$expected = '379B663EA3EBAA7DF78B2B2666190BBBABE5E11B221428C503C49C09310C7F78'
$required = @('Windows.WinMD', 'Windows.Foundation.FoundationContract.winmd', 'Windows.Foundation.UniversalApiContract.winmd')
New-Item -ItemType Directory -Force -Path (Join-Path $root 'build') | Out-Null
if (!(Test-Path $package)) {
    Invoke-WebRequest 'https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.contracts/10.0.19041.2/microsoft.windows.sdk.contracts.10.0.19041.2.nupkg' -OutFile $package
}
if ((Get-FileHash $package -Algorithm SHA256).Hash -ne $expected) { throw 'La dependencia de Windows SDK no supera la comprobación SHA-256.' }
if (@($required | Where-Object { !(Test-Path (Join-Path $extract (Join-Path 'ref\netstandard2.0' $_))) }).Count -ne 0) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $expectedExtract = [IO.Path]::GetFullPath((Join-Path $root 'build\winrt-contracts'))
    if ([IO.Path]::GetFullPath($extract) -ne $expectedExtract -or !$expectedExtract.StartsWith([IO.Path]::GetFullPath($root) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Ruta de extracción inesperada.'
    }
    if (Test-Path $extract) { Remove-Item -LiteralPath $extract -Recurse -Force }
    [IO.Compression.ZipFile]::ExtractToDirectory($package, $extract)
}
foreach ($name in $required) {
    if (!(Test-Path (Join-Path $extract (Join-Path 'ref\netstandard2.0' $name)))) { throw "Falta $name en la dependencia de Windows SDK." }
}
Write-Output (Join-Path $extract 'ref\netstandard2.0')
