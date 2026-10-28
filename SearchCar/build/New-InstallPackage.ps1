<#
.SYNOPSIS
    Tworzy paczkę instalacyjną aplikacji SearchCar.

.DESCRIPTION
    1. (opcjonalnie) uruchamia testy,
    2. publikuje aplikację jako self-contained (nie wymaga zainstalowanego .NET ani Windows App SDK),
    3. tworzy paczkę ZIP z instalatorem dla bieżącego użytkownika (bez uprawnień administratora):
       Instaluj.cmd / Install.ps1 / Uninstall.ps1 - skróty w menu Start i na pulpicie, wpis w "Aplikacje i funkcje",
    4. z przełącznikiem -Inno dodatkowo buduje SearchCar-Setup-<wersja>.exe przy pomocy Inno Setup (jeśli jest zainstalowany).

.PARAMETER Version
    Wersja aplikacji, np. 1.2.0. Trafia do właściwości pliku exe, nazwy paczki i wpisu w "Aplikacje i funkcje".

.PARAMETER OutputDir
    Katalog wynikowy (domyślnie artifacts\ w katalogu repozytorium).

.PARAMETER SkipTests
    Pomija uruchamianie testów.

.PARAMETER Inno
    Buduje również instalator .exe (wymaga Inno Setup 6: winget install JRSoftware.InnoSetup).

.EXAMPLE
    .\build\New-InstallPackage.ps1 -Version 1.0.0
.EXAMPLE
    .\build\New-InstallPackage.ps1 -Version 1.1.0 -Inno -SkipTests
#>
[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(\.\d+)?$')]
    [string] $Version = '1.0.0',
    [string] $OutputDir,
    [switch] $SkipTests,
    [switch] $Inno
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot   = Split-Path -Parent $PSScriptRoot
$AppProject = Join-Path $RepoRoot 'src\SearchCar.App\SearchCar.App.csproj'
$TestProject = Join-Path $RepoRoot 'tests\SearchCar.Core.Tests\SearchCar.Core.Tests.csproj'
if (-not $OutputDir) { $OutputDir = Join-Path $RepoRoot 'artifacts' }

$AppName    = 'SearchCar'
$Publisher  = 'SearchCar'
$ExeName    = 'SearchCar.exe'
$Runtime    = 'win-x64'
$Utf8Bom    = New-Object System.Text.UTF8Encoding $true   # Windows PowerShell 5.1 needs the BOM to read Polish characters

function Write-Step([string] $Text) { Write-Host "==> $Text" -ForegroundColor Cyan }

function Invoke-Native([string] $File, [string[]] $Arguments) {
    & $File @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$File zakończył się kodem $LASTEXITCODE" }
}

function Write-TextFile([string] $Path, [string] $Content) {
    [System.IO.File]::WriteAllText($Path, $Content, $Utf8Bom)
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Nie znaleziono polecenia dotnet - zainstaluj .NET 9 SDK.' }

$StageDir   = Join-Path $OutputDir "stage\$AppName-$Version"
$PublishDir = Join-Path $StageDir 'app'
$ZipPath    = Join-Path $OutputDir "$AppName-$Version-$Runtime.zip"

# ------------------------------------------------------------------ 1. testy
if (-not $SkipTests) {
    Write-Step 'Uruchamiam testy'
    Invoke-Native dotnet @('test', $TestProject, '-c', 'Release', '--nologo', '-v', 'q')
}

# ------------------------------------------------------------------ 2. publikacja
Write-Step "Publikuję $AppName $Version ($Runtime, self-contained)"
if (Test-Path $StageDir) { Remove-Item $StageDir -Recurse -Force }
New-Item -ItemType Directory -Force $PublishDir | Out-Null

Invoke-Native dotnet @(
    'publish', $AppProject,
    '-c', 'Release',
    '-r', $Runtime,
    '-p:Platform=x64',
    '--self-contained', 'true',
    "-p:Version=$Version",
    '-p:WindowsAppSDKSelfContained=true',
    '-p:PublishReadyToRun=true',
    '-o', $PublishDir,
    '--nologo', '-v', 'q'
)

foreach ($required in @($ExeName, 'Web\map.html')) {
    if (-not (Test-Path (Join-Path $PublishDir $required))) { throw "Brak pliku $required w publikacji - sprawdź projekt." }
}
Get-ChildItem $PublishDir -Filter '*.pdb' -Recurse | Remove-Item -Force

# ------------------------------------------------------------------ 3. instalator per-user (ZIP)
Write-Step 'Tworzę skrypty instalacyjne'

$installPs1 = @'
<#
    Instalator SearchCar dla bieżącego użytkownika (nie wymaga uprawnień administratora).
    Użycie:  Instaluj.cmd   albo   powershell -ExecutionPolicy Bypass -File Install.ps1 [-NoDesktopShortcut] [-Quiet]
#>
param([switch] $NoDesktopShortcut, [switch] $Quiet)
$ErrorActionPreference = 'Stop'

$AppName   = '__APPNAME__'
$Version   = '__VERSION__'
$Publisher = '__PUBLISHER__'
$ExeName   = '__EXENAME__'

$source     = Join-Path $PSScriptRoot 'app'
$installDir = Join-Path $env:LOCALAPPDATA "Programs\$AppName"
$exe        = Join-Path $installDir $ExeName
$startMenu  = Join-Path ([Environment]::GetFolderPath('Programs')) "$AppName.lnk"
$desktop    = Join-Path ([Environment]::GetFolderPath('Desktop')) "$AppName.lnk"
$uninstKey  = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppName"

if (-not (Test-Path (Join-Path $source $ExeName))) { throw "Nie znaleziono plików aplikacji w $source" }

if ([Environment]::OSVersion.Version.Build -lt 17763) { throw 'SearchCar wymaga Windows 10 w wersji 1809 lub nowszej.' }

Write-Host "Instaluję $AppName $Version w $installDir"

# Zamknij działającą aplikację, żeby można było podmienić pliki.
Get-Process -Name ([IO.Path]::GetFileNameWithoutExtension($ExeName)) -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -like "$installDir*" } | Stop-Process -Force
Start-Sleep -Milliseconds 500

if (Test-Path $installDir) { Remove-Item $installDir -Recurse -Force }
New-Item -ItemType Directory -Force $installDir | Out-Null
Copy-Item (Join-Path $source '*') $installDir -Recurse -Force
Copy-Item (Join-Path $PSScriptRoot 'Uninstall.ps1') $installDir -Force

function New-Shortcut([string] $Path) {
    $shell = New-Object -ComObject WScript.Shell
    $lnk = $shell.CreateShortcut($Path)
    $lnk.TargetPath = $exe
    $lnk.WorkingDirectory = $installDir
    $lnk.IconLocation = "$exe,0"
    $lnk.Description = 'Wyszukiwarka samochodów z weryfikacją VIN'
    $lnk.Save()
}
New-Shortcut $startMenu
if (-not $NoDesktopShortcut) { New-Shortcut $desktop }

# Wpis w "Aplikacje i funkcje" (Ustawienia -> Aplikacje).
$sizeKb = [int]((Get-ChildItem $installDir -Recurse -File | Measure-Object Length -Sum).Sum / 1KB)
New-Item -Path $uninstKey -Force | Out-Null
$uninstall = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$installDir\Uninstall.ps1`""
$values = @{
    DisplayName     = $AppName
    DisplayVersion  = $Version
    Publisher       = $Publisher
    InstallLocation = $installDir
    DisplayIcon     = "$exe,0"
    UninstallString = $uninstall
    QuietUninstallString = "$uninstall -Quiet"
    InstallDate     = (Get-Date -Format 'yyyyMMdd')
}
foreach ($k in $values.Keys) { New-ItemProperty -Path $uninstKey -Name $k -Value $values[$k] -PropertyType String -Force | Out-Null }
New-ItemProperty -Path $uninstKey -Name EstimatedSize -Value $sizeKb -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $uninstKey -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $uninstKey -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null

# Mapa w aplikacji korzysta z WebView2 (na Windows 11 jest zawsze zainstalowany).
$webView2 = @(
    'HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}',
    'HKLM:\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}',
    'HKCU:\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}'
) | Where-Object { Test-Path $_ }
if (-not $webView2) {
    Write-Warning 'Nie wykryto Microsoft Edge WebView2 Runtime - mapa nie będzie działać. Pobierz: https://go.microsoft.com/fwlink/p/?LinkId=2124703'
}

Write-Host "Zainstalowano. Skrót: menu Start -> $AppName" -ForegroundColor Green
if (-not $Quiet) {
    $answer = Read-Host 'Uruchomić teraz? (T/n)'
    if ($answer -eq '' -or $answer -match '^[tTyY]') { Start-Process $exe -WorkingDirectory $installDir }
}
'@

$uninstallPs1 = @'
<#
    Odinstalowuje SearchCar zainstalowany dla bieżącego użytkownika.
    -RemoveUserData usuwa też dane w %LOCALAPPDATA%\SearchCar (pamięć podręczna mapy i listy marek).
#>
param([switch] $RemoveUserData, [switch] $Quiet)
$ErrorActionPreference = 'Stop'

$AppName    = '__APPNAME__'
$ExeName    = '__EXENAME__'
$installDir = Join-Path $env:LOCALAPPDATA "Programs\$AppName"

if (-not $Quiet) {
    $answer = Read-Host "Odinstalować $AppName? (T/n)"
    if (-not ($answer -eq '' -or $answer -match '^[tTyY]')) { return }
}

Get-Process -Name ([IO.Path]::GetFileNameWithoutExtension($ExeName)) -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -like "$installDir*" } | Stop-Process -Force
Start-Sleep -Milliseconds 500

Remove-Item (Join-Path ([Environment]::GetFolderPath('Programs')) "$AppName.lnk") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path ([Environment]::GetFolderPath('Desktop')) "$AppName.lnk") -Force -ErrorAction SilentlyContinue
Remove-Item "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppName" -Recurse -Force -ErrorAction SilentlyContinue

# Ten skrypt leży w katalogu instalacji; PowerShell ma go już wczytanego, więc katalog można usunąć.
Set-Location $env:TEMP
Remove-Item $installDir -Recurse -Force -ErrorAction SilentlyContinue

if ($RemoveUserData) {
    Remove-Item (Join-Path $env:LOCALAPPDATA $AppName) -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "$AppName został odinstalowany." -ForegroundColor Green
'@

$installCmd = @'
@echo off
rem Instalator SearchCar - dwuklik uruchamia instalację dla bieżącego użytkownika.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install.ps1" %*
if errorlevel 1 pause
'@

function Expand-Template([string] $Text) {
    $Text.Replace('__APPNAME__', $AppName).Replace('__VERSION__', $Version).Replace('__PUBLISHER__', $Publisher).Replace('__EXENAME__', $ExeName)
}

Write-TextFile (Join-Path $StageDir 'Install.ps1')   (Expand-Template $installPs1)
Write-TextFile (Join-Path $StageDir 'Uninstall.ps1') (Expand-Template $uninstallPs1)
[System.IO.File]::WriteAllText((Join-Path $StageDir 'Instaluj.cmd'), $installCmd.Replace("`r`n", "`n").Replace("`n", "`r`n"), [System.Text.Encoding]::ASCII)
Write-TextFile (Join-Path $StageDir 'CZYTAJ.txt') @"
$AppName $Version - wyszukiwarka samochodów z weryfikacją VIN

Instalacja:      dwuklik na Instaluj.cmd (instalacja dla bieżącego użytkownika, bez uprawnień administratora)
Odinstalowanie:  Ustawienia -> Aplikacje -> $AppName -> Odinstaluj
Wymagania:       Windows 10 1809+ / Windows 11 x64, Microsoft Edge WebView2 Runtime, dostęp do internetu
"@

Write-Step "Pakuję $ZipPath"
New-Item -ItemType Directory -Force $OutputDir | Out-Null
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
Compress-Archive -Path (Join-Path $StageDir '*') -DestinationPath $ZipPath -CompressionLevel Optimal

# ------------------------------------------------------------------ 4. Inno Setup (opcjonalnie)
$SetupPath = $null
if ($Inno) {
    $iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
    if (-not $iscc) {
        $iscc = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
                  "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
    }
    if (-not $iscc) {
        Write-Warning 'Nie znaleziono Inno Setup 6 - pomijam Setup.exe. Instalacja: winget install JRSoftware.InnoSetup'
    }
    else {
        Write-Step 'Buduję instalator Inno Setup'
        $iss = @"
#define AppName "$AppName"
#define AppVersion "$Version"

[Setup]
AppId={{6E1C3A52-5B5F-4E0B-9C1E-4A8C2F1D7B10}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=$Publisher
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=$OutputDir
OutputBaseFilename={#AppName}-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\$ExeName
CloseApplications=yes

[Languages]
Name: "polish"; MessagesFile: "compiler:Languages\Polish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "$PublishDir\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\$ExeName"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\$ExeName"; Tasks: desktopicon

[Run]
Filename: "{app}\$ExeName"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
"@
        $issPath = Join-Path $OutputDir "stage\$AppName.iss"
        Write-TextFile $issPath $iss
        Invoke-Native $iscc @('/Q', $issPath)
        $SetupPath = Join-Path $OutputDir "$AppName-Setup-$Version.exe"
    }
}

# ------------------------------------------------------------------ podsumowanie
$zipMb = [math]::Round((Get-Item $ZipPath).Length / 1MB, 1)
Write-Host ''
Write-Host 'Gotowe.' -ForegroundColor Green
Write-Host "  Paczka ZIP:  $ZipPath ($zipMb MB)"
Write-Host '               rozpakuj i uruchom Instaluj.cmd'
if ($SetupPath) { Write-Host "  Instalator:  $SetupPath" }
