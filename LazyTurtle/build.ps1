<#
.SYNOPSIS
    Builds Lazy Turtle and creates the install package.

.DESCRIPTION
    1. Builds src\LazyTurtle (WinForms, .NET Framework 4.0) in Release.
    2. Restores the WiX 5 command line tool (local dotnet tool, see .config\dotnet-tools.json).
    3. Builds installer\Package.wxs into out\LazyTurtle-<version>.msi  (x86, Windows 7 SP1 .. Windows 11).
    4. Creates out\LazyTurtle-<version>-portable.zip (just unzip and run LazyTurtle.exe).

    Requirements on the build machine: .NET SDK 8+ and the .NET Framework 4.0 targeting pack
    (installed with Visual Studio). Internet access to nuget.org is needed once to fetch WiX.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\build.ps1
    powershell -ExecutionPolicy Bypass -File .\build.ps1 -Version 2.1.0
#>
param(
    [string]$Version = '4.0.0'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root 'src\LazyTurtle\LazyTurtle.csproj'
$binDir = Join-Path $root 'src\LazyTurtle\bin\Release\net40'
$outDir = Join-Path $root 'out'
$wixVersion = '5.0.2'

Push-Location $root
try {
    Write-Host "==> Building Lazy Turtle $Version" -ForegroundColor Cyan
    dotnet build $project -c Release -nologo "-p:Version=$Version"
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed ($LASTEXITCODE)" }

    Write-Host '==> Restoring WiX toolset' -ForegroundColor Cyan
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed ($LASTEXITCODE)" }
    dotnet wix extension add "WixToolset.UI.wixext/$wixVersion"
    if ($LASTEXITCODE -ne 0) { throw "wix extension add failed ($LASTEXITCODE)" }

    Write-Host '==> Building MSI' -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
    $msi = Join-Path $outDir "LazyTurtle-$Version.msi"
    dotnet wix build (Join-Path $root 'installer\Package.wxs') `
        -arch x86 `
        -ext WixToolset.UI.wixext `
        -d "Version=$Version" `
        -d "BinDir=$binDir" `
        -d "IconFile=$(Join-Path $root 'src\LazyTurtle\Assets\app.ico')" `
        -d "LicenseFile=$(Join-Path $root 'installer\License.rtf')" `
        -o $msi
    if ($LASTEXITCODE -ne 0) { throw "wix build failed ($LASTEXITCODE)" }
    Remove-Item (Join-Path $outDir '*.wixpdb') -ErrorAction SilentlyContinue

    Write-Host '==> Building portable ZIP' -ForegroundColor Cyan
    $zip = Join-Path $outDir "LazyTurtle-$Version-portable.zip"
    $stage = Join-Path $outDir 'portable'
    Remove-Item $stage, $zip -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $stage | Out-Null
    Copy-Item (Join-Path $binDir 'LazyTurtle.exe'), (Join-Path $binDir 'LazyTurtle.exe.config') $stage
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip)
    Remove-Item $stage -Recurse -Force

    Write-Host "==> Done:" -ForegroundColor Green
    Write-Host "    $msi"
    Write-Host "    $zip"
}
finally {
    Pop-Location
}
