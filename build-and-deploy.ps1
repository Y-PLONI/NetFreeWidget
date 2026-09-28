# Build and Deploy Script for NetFree Widget
# This script builds the solution and deploys the MSIX package for testing

param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    
    [Parameter()]
    [ValidateSet('x64', 'x86', 'ARM64')]
    [string]$Platform = 'x64'
)

Write-Host "🔨 Building NetFree Widget..." -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Gray
Write-Host "Platform: $Platform" -ForegroundColor Gray
Write-Host ""

# Check if Visual Studio is installed
$msbuildPath = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" `
    -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe `
    -prerelease | Select-Object -First 1

if (-not $msbuildPath) {
    Write-Host "❌ Visual Studio 2022 not found!" -ForegroundColor Red
    Write-Host "Please install Visual Studio 2022 with .NET Desktop Development workload." -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ Found MSBuild: $msbuildPath" -ForegroundColor Green
Write-Host ""

# Restore NuGet packages
Write-Host "📦 Restoring NuGet packages..." -ForegroundColor Cyan
& $msbuildPath NetFreeWidget.sln /t:Restore /p:Configuration=$Configuration /p:Platform=$Platform /v:minimal

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ NuGet restore failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ NuGet packages restored" -ForegroundColor Green
Write-Host ""

# Build the solution
Write-Host "🔨 Building solution..." -ForegroundColor Cyan
& $msbuildPath NetFreeWidget.sln /p:Configuration=$Configuration /p:Platform=$Platform /v:minimal

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Build succeeded" -ForegroundColor Green
Write-Host ""

# Deploy the unpacked package for testing without requiring a certificate
Write-Host "📦 Deploying Appx package..." -ForegroundColor Cyan
$manifestPath = "NetFreeBoardWidgetPackage\bin\$Platform\$Configuration\AppxManifest.xml"

if (-not (Test-Path $manifestPath)) {
    Write-Host "❌ AppxManifest.xml not found at: $manifestPath" -ForegroundColor Red
    exit 1
}

# Use Add-AppxPackage to install unpacked layout
try {
    # Registering the same version from another folder (e.g. Debug vs Release) silently keeps the old one.
    $layoutDir = (Resolve-Path (Split-Path $manifestPath)).Path
    $existing = Get-AppxPackage NetFreeWidget
    if ($existing -and $existing.InstallLocation -ne $layoutDir) {
        Write-Host "♻️ Removing package registered from $($existing.InstallLocation)" -ForegroundColor Yellow
        Get-Process NetFreeBoardWidgetProvider -ErrorAction SilentlyContinue | Stop-Process -Force
        Remove-AppxPackage $existing.PackageFullName
    }


    Add-AppxPackage -Register $manifestPath
    Write-Host "✅ Package deployed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "🎉 Done! You can now:" -ForegroundColor Cyan
    Write-Host "   1. Press WIN + W to open Widgets Board" -ForegroundColor White
    Write-Host "   2. Click + next to your avatar" -ForegroundColor White
    Write-Host "   3. Search for 'גלישה - נטפרי'" -ForegroundColor White
    Write-Host "   4. Add the widget" -ForegroundColor White
}
catch {
    Write-Host "❌ Deployment failed: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "💡 Make sure Developer Mode is enabled:" -ForegroundColor Yellow
    Write-Host "   Settings → Privacy & Security → For developers → Developer Mode" -ForegroundColor White
    exit 1
}
