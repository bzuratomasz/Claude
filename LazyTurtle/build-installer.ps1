<#
.SYNOPSIS
    Creates the Lazy Turtle installation package (MSI + Setup.exe).

.DESCRIPTION
    1. Packages the game with build.ps1 (Shipping, Win64), unless -SkipBuild reuses out\package.
    2. Restores the WiX 5 command line tool (local dotnet tool, see .config\dotnet-tools.json).
    3. Builds installer\Package.wxs into out\LazyTurtle-<version>.msi (x64, Windows 10 / 11).
    4. Builds installer\Bundle.wxs into out\LazyTurtle-<version>-Setup.exe, which installs the Unreal
       prerequisites (Visual C++ runtime) when missing and then the MSI. Give this one to users.

    Requirements: everything build.ps1 needs, plus internet access to nuget.org once to fetch WiX.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\build-installer.ps1
    powershell -ExecutionPolicy Bypass -File .\build-installer.ps1 -Version 5.0.1 -EngineDir "D:\Epic Games\UE_5.8"
    powershell -ExecutionPolicy Bypass -File .\build-installer.ps1 -SkipBuild
#>
param(
    [string]$Version = '5.0.0',
    [string]$EngineDir = '',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$outDir = Join-Path $root 'out'
$wixVersion = '5.0.2'
$icon = Join-Path $root 'Build\Windows\Application.ico'
$license = Join-Path $root 'installer\License.rtf'

Push-Location $root
try {
    if (-not $SkipBuild) {
        $buildArgs = @{ Version = $Version }
        if ($EngineDir) { $buildArgs.EngineDir = $EngineDir }
        & (Join-Path $root 'build.ps1') @buildArgs
    }

    # the packaged game: the folder in out\package that holds LazyTurtle.exe
    $exe = Get-ChildItem (Join-Path $outDir 'package') -Filter 'LazyTurtle.exe' -Recurse -Depth 2 -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if (-not $exe) { throw 'No packaged game in out\package. Run without -SkipBuild first.' }
    $stageDir = $exe.DirectoryName

    # UE 5.8 stages the Visual C++ x64 runtime installer; older engines shipped UEPrereqSetup_x64.exe
    $prereq = $null
    foreach ($dir in @($stageDir, $EngineDir) | Where-Object { $_ }) {
        foreach ($name in 'vc_redist.x64.exe', 'UEPrereqSetup_x64.exe') {
            $candidate = Join-Path $dir "Engine\Extras\Redist\en-us\$name"
            if (-not $prereq -and (Test-Path $candidate)) { $prereq = $candidate }
        }
    }
    if (-not $prereq) { throw "vc_redist.x64.exe / UEPrereqSetup_x64.exe not found (looked in the packaged game and -EngineDir)." }

    Write-Host '==> Restoring WiX toolset' -ForegroundColor Cyan
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed ($LASTEXITCODE)" }
    foreach ($ext in 'WixToolset.UI.wixext', 'WixToolset.BootstrapperApplications.wixext', 'WixToolset.Util.wixext') {
        dotnet wix extension add "$ext/$wixVersion"
        if ($LASTEXITCODE -ne 0) { throw "wix extension add $ext failed ($LASTEXITCODE)" }
    }

    New-Item -ItemType Directory -Path $outDir -Force | Out-Null

    Write-Host '==> Building MSI' -ForegroundColor Cyan
    $msi = Join-Path $outDir "LazyTurtle-$Version.msi"
    dotnet wix build (Join-Path $root 'installer\Package.wxs') `
        -arch x64 `
        -ext WixToolset.UI.wixext `
        -d "Version=$Version" `
        -d "StageDir=$stageDir" `
        -d "IconFile=$icon" `
        -d "LicenseFile=$license" `
        -o $msi
    if ($LASTEXITCODE -ne 0) { throw "wix build (MSI) failed ($LASTEXITCODE)" }

    Write-Host '==> Building Setup.exe' -ForegroundColor Cyan
    $setup = Join-Path $outDir "LazyTurtle-$Version-Setup.exe"
    dotnet wix build (Join-Path $root 'installer\Bundle.wxs') `
        -arch x64 `
        -ext WixToolset.BootstrapperApplications.wixext `
        -ext WixToolset.Util.wixext `
        -d "Version=$Version" `
        -d "MsiFile=$msi" `
        -d "PrereqFile=$prereq" `
        -d "IconFile=$icon" `
        -d "LicenseFile=$license" `
        -o $setup
    if ($LASTEXITCODE -ne 0) { throw "wix build (bundle) failed ($LASTEXITCODE)" }
    Remove-Item (Join-Path $outDir '*.wixpdb') -ErrorAction SilentlyContinue

    Write-Host '==> Done:' -ForegroundColor Green
    Write-Host "    $setup   (give this to users)"
    Write-Host "    $msi"
}
finally {
    Pop-Location
}
