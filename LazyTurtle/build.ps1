<#
.SYNOPSIS
    Builds and packages Lazy Turtle (Unreal Engine 5.8, Win64).

.DESCRIPTION
    1. Regenerates the quiz data (Content\Data\*.json) with tools\QuizExport (needs the .NET 8 SDK).
    2. Builds, cooks and packages the game with Unreal Automation Tool (Shipping, Win64).
    3. Zips the packaged game into out\LazyTurtle-<version>-win64.zip.

    Requirements: Unreal Engine 5.8 (Epic Games Launcher or source build), Visual Studio 2022 with the
    "Game development with C++" workload, .NET 8 SDK.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\build.ps1
    powershell -ExecutionPolicy Bypass -File .\build.ps1 -EngineDir "D:\Epic Games\UE_5.8" -Version 5.0.1
#>
param(
    [string]$Version = '5.0.0',
    [string]$EngineDir = '',
    [switch]$SkipQuizExport
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root 'LazyTurtle.uproject'
$outDir = Join-Path $root 'out'
$stage = Join-Path $outDir 'package'

if (-not $EngineDir) {
    # Epic Games Launcher installs register themselves here
    $key = 'HKLM:\SOFTWARE\EpicGames\Unreal Engine\5.8'
    if (Test-Path $key) { $EngineDir = (Get-ItemProperty $key).InstalledDirectory }
}
if (-not $EngineDir -or -not (Test-Path $EngineDir)) {
    throw 'Unreal Engine 5.8 not found. Install it from the Epic Games Launcher or pass -EngineDir "<path to UE_5.8>".'
}
$uat = Join-Path $EngineDir 'Engine\Build\BatchFiles\RunUAT.bat'

Push-Location $root
try {
    if (-not $SkipQuizExport) {
        Write-Host '==> Exporting quiz data' -ForegroundColor Cyan
        dotnet run --project (Join-Path $root 'tools\QuizExport') -c Release -- (Join-Path $root 'Content\Data')
        if ($LASTEXITCODE -ne 0) { throw "quiz export failed ($LASTEXITCODE)" }
    }

    Write-Host "==> Packaging Lazy Turtle $Version" -ForegroundColor Cyan
    Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
    & $uat BuildCookRun "-project=$project" -noP4 -platform=Win64 -clientconfig=Shipping `
        -build -cook -stage -pak -iostore -package -archive "-archivedirectory=$stage" -prereqs -nodebuginfo -utf8output
    if ($LASTEXITCODE -ne 0) { throw "BuildCookRun failed ($LASTEXITCODE)" }

    Write-Host '==> Zipping' -ForegroundColor Cyan
    $zip = Join-Path $outDir "LazyTurtle-$Version-win64.zip"
    Remove-Item $zip -ErrorAction SilentlyContinue
    $packaged = Get-ChildItem $stage -Directory | Select-Object -First 1
    Compress-Archive -Path (Join-Path $packaged.FullName '*') -DestinationPath $zip

    Write-Host '==> Done:' -ForegroundColor Green
    Write-Host "    $zip"
}
finally {
    Pop-Location
}
