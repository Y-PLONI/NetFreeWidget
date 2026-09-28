# Run by SetupSteps.InstallPackage, which puts "$AppInstallerUri = '...'" in front of this text.
# Exit codes: 0 installed through the .appinstaller (automatic updates registered),
#             2 installed from files downloaded here (no automatic updates), 1 failed.
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8

try {
    Add-AppxPackage -Path $AppInstallerUri -AppInstallerFile -ForceTargetApplicationShutdown
    exit 0
}
catch {
    $appInstallerError = $_.Exception.Message
}

# App Installer downloads through Delivery Optimization, which fails on some filtered networks (NetFree:
# 0x80D05011) where a plain HTTPS download works. Download the same files here and install them locally.
$dir = Join-Path ([IO.Path]::GetTempPath()) "widget-setup-$([guid]::NewGuid().ToString('N'))"
try {
    New-Item -ItemType Directory $dir | Out-Null
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    $web = New-Object Net.WebClient

    function Get-File([string]$uri) {
        $file = Join-Path $dir ([IO.Path]::GetFileName(([Uri]$uri).LocalPath))
        $web.DownloadFile($uri, $file)
        $file
    }

    $manifest = [xml][IO.File]::ReadAllText((Get-File $AppInstallerUri))
    $main = $manifest.AppInstaller.MainPackage

    # Only dependencies that are missing or older than required: passing one that is already installed in a
    # newer version fails with 0x80073D02 (apps that use it would have to close).
    $deps = @()
    foreach ($p in @($manifest.AppInstaller.Dependencies.Package)) {
        if (-not $p) { continue }
        $installed = Get-AppxPackage -Name $p.Name |
            Where-Object { "$($_.Architecture)" -eq $p.ProcessorArchitecture -and [version]$_.Version -ge [version]$p.Version }
        if (-not $installed) { $deps += Get-File $p.Uri }
    }

    $mainFile = Get-File $main.Uri
    if ($deps) { Add-AppxPackage -Path $mainFile -DependencyPath $deps -ForceTargetApplicationShutdown }
    else { Add-AppxPackage -Path $mainFile -ForceTargetApplicationShutdown }
    Write-Output $appInstallerError
    exit 2
}
catch {
    Write-Output $appInstallerError
    Write-Output $_.Exception.Message
    exit 1
}
finally {
    Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue
}
