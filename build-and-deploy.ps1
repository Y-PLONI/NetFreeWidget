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

# A running provider locks its exe in the layout folder; Windows relaunches it on the next widget activation.
Get-Process NetFreeBoardWidgetProvider -ErrorAction SilentlyContinue | Stop-Process -Force

# Build the solution
Write-Host "🔨 Building solution..." -ForegroundColor Cyan
& $msbuildPath NetFreeWidget.sln /p:Configuration=$Configuration /p:Platform=$Platform /v:minimal

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Build succeeded" -ForegroundColor Green
Write-Host ""

# Deploy the unpacked package for testing without requiring a certificate.
# The intermediate folder bin\<Platform>\<Configuration> holds only the manifest and the exe (no assets, no full
# resources.pri), so the widget picker had no icon. Register an extracted copy of the built .msix instead.
Write-Host "📦 Deploying Appx package..." -ForegroundColor Cyan
$packageRoot = Join-Path $PSScriptRoot 'NetFreeBoardWidgetPackage'
$msix = Get-ChildItem (Join-Path $packageRoot 'AppPackages') -Recurse -Filter "*_$Platform.msix" -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '\\Dependencies\\' } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $msix) {
    Write-Host "❌ No .msix found under $packageRoot\AppPackages" -ForegroundColor Red
    exit 1
}

$layoutDir = Join-Path $packageRoot "bin\Layout\$Platform-$Configuration"

try {
    Get-Process NetFreeBoardWidgetProvider -ErrorAction SilentlyContinue | Stop-Process -Force

    # Registering the same version from another folder silently keeps the old one, so remove it first.
    # Files a packaged app creates under AppData live in the package's private folder and die with it: keep a copy.
    # A changed Publisher is a different package identity, so it must go too.
    $publisher = ([xml](Get-Content (Join-Path $packageRoot 'Package.appxmanifest') -Raw)).Package.Identity.Publisher
    $existing = Get-AppxPackage NetFreeWidget
    $backup = $null
    if ($existing -and ($existing.InstallLocation -ne $layoutDir -or $existing.Publisher -ne $publisher)) {
        $dataDir = Join-Path $env:LOCALAPPDATA "Packages\$($existing.PackageFamilyName)\LocalCache\Roaming\NetFreeWidget"
        if (Test-Path $dataDir) {
            $backup = Join-Path ([IO.Path]::GetTempPath()) "NetFreeWidget-data-$([guid]::NewGuid().ToString('N'))"
            Copy-Item $dataDir $backup -Recurse
        }
        Write-Host "♻️ Removing package registered from $($existing.InstallLocation)" -ForegroundColor Yellow
        Remove-AppxPackage $existing.PackageFullName
    }

    if (Test-Path $layoutDir) { Remove-Item $layoutDir -Recurse -Force }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($msix.FullName, $layoutDir)

    Add-AppxPackage -Register (Join-Path $layoutDir 'AppxManifest.xml') -ForceApplicationShutdown

    if ($backup) {
        $pfn = (Get-AppxPackage NetFreeWidget).PackageFamilyName
        $target = Join-Path $env:LOCALAPPDATA "Packages\$pfn\LocalCache\Roaming"
        New-Item -ItemType Directory -Force $target | Out-Null
        Copy-Item $backup (Join-Path $target 'NetFreeWidget') -Recurse -Force
        Remove-Item $backup -Recurse -Force
        Write-Host "✅ Widget data restored" -ForegroundColor Green
    }

    Write-Host "✅ Package deployed successfully from $layoutDir" -ForegroundColor Green
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
