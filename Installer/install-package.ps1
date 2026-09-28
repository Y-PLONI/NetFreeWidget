# Run by SetupSteps.InstallPackage, which puts "$AppInstallerUri = '...'" (and, for tests,
# "$ForceFallback = $true") in front of this text.
# Exit codes: 0 installed through the .appinstaller,
#             2 installed from files downloaded here (see below), 1 failed.
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8

$appInstallerError = 'skipped (test of the fallback)'
if (-not $ForceFallback) {
    try {
        Add-AppxPackage -Path $AppInstallerUri -AppInstallerFile -ForceTargetApplicationShutdown
        exit 0
    }
    catch {
        $appInstallerError = $_.Exception.Message
    }
}

# App Installer downloads through Delivery Optimization, which fails on some filtered networks (NetFree:
# 0x80D05011) where a plain HTTPS download works. Download the same files here and install them from a
# local copy of the .appinstaller whose Uri is still the published one, so the package stays registered to
# it; the widget then updates itself the same way (SelfUpdater).
$dir = Join-Path ([IO.Path]::GetTempPath()) "widget-setup-$([guid]::NewGuid().ToString('N'))"
try {
    New-Item -ItemType Directory $dir | Out-Null
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    $web = New-Object Net.WebClient

    function Get-File([string]$uri) {
        $source = New-Object Uri ([Uri]$AppInstallerUri), $uri
        $file = Join-Path $dir ([IO.Path]::GetFileName($source.LocalPath))
        $web.DownloadFile($source, $file)
        $file
    }

    $manifest = [xml][IO.File]::ReadAllText((Get-File $AppInstallerUri))
    $main = $manifest.AppInstaller.MainPackage
    $depsNode = $manifest.AppInstaller.Dependencies

    # Only dependencies that are missing or older than required: passing one that is already installed in a
    # newer version fails with 0x80073D02 (apps that use it would have to close).
    $deps = @()
    foreach ($p in @($depsNode.Package)) {
        if (-not $p) { continue }
        $installed = Get-AppxPackage -Name $p.Name |
            Where-Object { "$($_.Architecture)" -eq $p.ProcessorArchitecture -and [version]$_.Version -ge [version]$p.Version }
        if ($installed) { [void]$depsNode.RemoveChild($p); continue }
        $file = Get-File $p.Uri
        $deps += $file
        $p.Uri = ([Uri]$file).AbsoluteUri
    }
    if ($depsNode -and -not $depsNode.HasChildNodes) { [void]$manifest.AppInstaller.RemoveChild($depsNode) }

    $mainFile = Get-File $main.Uri
    $main.Uri = ([Uri]$mainFile).AbsoluteUri
    $local = Join-Path $dir 'local.appinstaller'
    $manifest.Save($local)
    Write-Output $appInstallerError
    try {
        Add-AppxPackage -Path $local -AppInstallerFile -ForceTargetApplicationShutdown
    }
    catch {
        # Last resort: the packages themselves, without the update registration.
        Write-Output "local .appinstaller: $($_.Exception.Message)"
        if ($deps) { Add-AppxPackage -Path $mainFile -DependencyPath $deps -ForceTargetApplicationShutdown }
        else { Add-AppxPackage -Path $mainFile -ForceTargetApplicationShutdown }
    }
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
