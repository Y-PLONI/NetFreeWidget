# NetFree Widget installer
# Run from the folder that holds the downloaded release files:
#   powershell -ExecutionPolicy Bypass -File .\install.ps1

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

# Importing into LocalMachine\TrustedPeople requires elevation; relaunch as admin if needed.
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Start-Process powershell -Verb RunAs -ArgumentList @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$($MyInvocation.MyCommand.Path)`"")
    exit
}

$cer = Join-Path $here 'NetFreeWidget.cer'
if (-not (Test-Path $cer)) { throw "Certificate not found: $cer" }

$msix = Get-ChildItem $here -Filter 'NetFreeWidget_*.msix' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $msix) { throw "No NetFreeWidget_*.msix found in $here" }

Write-Host 'Importing certificate to LocalMachine\TrustedPeople...'
Import-Certificate -FilePath $cer -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null

# Framework dependencies (Windows App Runtime). Failures here usually mean a same/newer version is already installed.
foreach ($dep in Get-ChildItem $here -Filter 'Dependency_*' -ErrorAction SilentlyContinue) {
    try {
        Write-Host "Installing dependency $($dep.Name)..."
        Add-AppxPackage -Path $dep.FullName
    }
    catch {
        Write-Host "  skipped: $($_.Exception.Message)" -ForegroundColor DarkYellow
    }
}

Write-Host "Installing $($msix.Name)..."
Add-AppxPackage -Path $msix.FullName -ForceUpdateFromAnyVersion

Write-Host ''
Write-Host 'Done. Open the Widgets board (Win+W), click +, and add the NetFree widget.' -ForegroundColor Green
Read-Host 'Press Enter to close'
