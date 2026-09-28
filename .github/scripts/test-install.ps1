# End-to-end tests of the installer and of automatic updates, run by CI on a clean Windows runner.
# Needs Windows PowerShell 5.1 (WinRT interop) and an elevated session (the runner is one).
#
#   -Mode Local   before publishing: install through NetFreeWidget-Setup.exe from a local copy of the
#                 release (file:// feed), then publish a fake newer version to that feed and prove that
#                 Windows sees it and updates in place.
#   -Mode Remote  after publishing: the real GitHub URLs. latest/download serves this version, every link
#                 works, the previous release upgrades to this one, and a fresh install through the
#                 downloaded installer lands on this version with updates wired to latest/download.
param(
    [Parameter(Mandatory)] [ValidateSet('Local', 'Remote')] [string]$Mode,
    [Parameter(Mandatory)] [string]$Version,
    [string]$ReleaseDir,       # Local: the folder with the release files
    [string]$Pfx,              # Local: signing certificate for the fake update
    [string]$PfxPassword,
    [string]$Repository,       # Remote: owner/name
    [string]$PackageName = 'NetFreeWidget',
    [string]$Publisher = 'CN=Y-PLONI'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$script:Failures = 0
$work = Join-Path ([IO.Path]::GetTempPath()) "widget-test-$Mode"
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory $work | Out-Null

# ---------- reporting ----------

function Report([string]$status, [string]$text) {
    $color = @{ PASS = 'Green'; FAIL = 'Red'; SKIP = 'Yellow'; INFO = 'Gray' }[$status]
    Write-Host "[$status] $text" -ForegroundColor $color
    if ($env:GITHUB_STEP_SUMMARY) {
        $icon = @{ PASS = '✅'; FAIL = '❌'; SKIP = '⏭️'; INFO = 'ℹ️' }[$status]
        Add-Content $env:GITHUB_STEP_SUMMARY "- $icon $text" -Encoding utf8
    }
}

function Check([string]$name, [bool]$ok, [string]$detail = '') {
    if ($ok) { Report PASS $name }
    else {
        $script:Failures++
        Report FAIL ($(if ($detail) { "$name — $detail" } else { $name }))
    }
}

# ---------- package state (WinRT) ----------

Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Management.Deployment.PackageManager, Windows.Management, ContentType = WindowsRuntime]
$null = [Windows.ApplicationModel.PackageUpdateAvailabilityResult, Windows.ApplicationModel, ContentType = WindowsRuntime]
$script:AsTask = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
    $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and
    $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' } | Select-Object -First 1

function Get-Package {
    $pm = New-Object Windows.Management.Deployment.PackageManager
    @($pm.FindPackagesForUser('', $PackageName, $Publisher)) | Select-Object -First 1
}

function Get-PackageVersion($pkg) {
    if (-not $pkg) { return $null }
    $v = $pkg.Id.Version
    "$($v.Major).$($v.Minor).$($v.Build).$($v.Revision)"
}

function Get-UpdateUri($pkg) {
    $info = $pkg.GetAppInstallerInfo()
    if ($info) { $info.Uri.AbsoluteUri } else { $null }
}

# What Windows itself asks before an automatic update: is the .appinstaller behind this package newer?
function Get-UpdateAvailability($pkg) {
    $task = $script:AsTask.MakeGenericMethod([Windows.ApplicationModel.PackageUpdateAvailabilityResult]).Invoke(
        $null, @($pkg.CheckUpdateAvailabilityAsync()))
    if (-not $task.Wait(180000)) { return 'Timeout' }
    $r = $task.Result
    if ($r.ExtendedError) { "$($r.Availability) ($($r.ExtendedError.Message))" } else { "$($r.Availability)" }
}

function Remove-TestPackage {
    $pkg = Get-Package
    if ($pkg) { Remove-AppxPackage $pkg.Id.FullName }
}

# ---------- actions ----------

function Invoke-Setup([string]$exe, [string]$appInstaller, [switch]$Fallback) {
    $log = Join-Path $work "setup-$([guid]::NewGuid().ToString('N')).log"
    $argList = @('--quiet', '--log', "`"$log`"")
    if ($appInstaller) { $argList += @('--appinstaller', "`"$appInstaller`"") }
    if ($Fallback) { $argList += '--test-fallback' }
    $p = Start-Process $exe -ArgumentList $argList -Wait -PassThru
    $script:SetupLog = if (Test-Path $log) { @(Get-Content $log -Encoding utf8) } else { @() }
    $script:SetupLog | ForEach-Object { Report INFO "setup: $_" }
    $p.ExitCode
}

# The widget's own updater (SelfUpdater), taken from inside a release package: the provider exe run as
# "--update <feed>" does exactly what the widget does in the background, and works outside the package.
function Get-Updater([string]$msix) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $dir = Join-Path $work "updater-$([guid]::NewGuid().ToString('N'))"
    [IO.Compression.ZipFile]::ExtractToDirectory($msix, $dir)
    Join-Path $dir 'NetFreeBoardWidgetProvider\NetFreeBoardWidgetProvider.exe'
}

function Invoke-Updater([string]$exe, [string]$feed) {
    $widgetLog = Join-Path $env:APPDATA 'NetFreeWidget\error_log.txt'
    $before = if (Test-Path $widgetLog) { @(Get-Content $widgetLog -Encoding utf8).Count } else { 0 }
    $p = Start-Process $exe -ArgumentList @('--update', "`"$feed`"") -Wait -PassThru
    if (Test-Path $widgetLog) {
        Get-Content $widgetLog -Encoding utf8 | Select-Object -Skip $before | ForEach-Object { Report INFO "widget: $_" }
    }
    $p.ExitCode
}

# GitHub redirects release downloads to signed storage URLs that refuse HEAD, so request a GET and stop at the headers.
Add-Type -AssemblyName System.Net.Http
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$script:Http = New-Object System.Net.Http.HttpClient

function Test-Url([string]$url) {
    try {
        $r = $script:Http.GetAsync($url, [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).Result
        try { return $r.IsSuccessStatusCode } finally { $r.Dispose() }
    }
    catch { return $false }
}

function Get-Xml([string]$url) {
    $file = Join-Path $work "$([guid]::NewGuid().ToString('N')).xml"
    Invoke-WebRequest $url -OutFile $file -UseBasicParsing
    [xml](Get-Content $file -Raw -Encoding utf8)
}

# ---------- Local ----------

function Invoke-LocalTests {
    $setup = Join-Path $ReleaseDir 'NetFreeWidget-Setup.exe'
    $feed = Join-Path $work 'feed'
    New-Item -ItemType Directory $feed | Out-Null
    Get-ChildItem $ReleaseDir -File | Where-Object { $_.Extension -in '.msix', '.appx' } | Copy-Item -Destination $feed
    $feedUrl = ([Uri]$feed).AbsoluteUri
    $manifestUrl = "$feedUrl/NetFreeWidget.appinstaller"

    # The generated .appinstaller, pointed at the local feed: same structure as the published one.
    $source = Get-Content (Join-Path $ReleaseDir 'NetFreeWidget.appinstaller') -Raw -Encoding utf8
    $xml = [xml]$source
    Check 'appinstaller: version matches the build' ($xml.AppInstaller.Version -eq $Version) $xml.AppInstaller.Version
    Check 'appinstaller: points at latest/download' ($xml.AppInstaller.Uri -like '*/releases/latest/download/NetFreeWidget.appinstaller')
    Check 'appinstaller: background update check enabled' ($null -ne $xml.AppInstaller.UpdateSettings.AutomaticBackgroundTask)
    Check 'appinstaller: update check on launch enabled' ($null -ne $xml.AppInstaller.UpdateSettings.OnLaunch)

    function Write-Feed([string]$version, [string]$msixName) {
        $x = [xml]$source
        $x.AppInstaller.Version = $version
        $x.AppInstaller.Uri = $manifestUrl
        $x.AppInstaller.MainPackage.Version = $version
        $x.AppInstaller.MainPackage.Uri = "$feedUrl/$msixName"
        foreach ($d in @($x.AppInstaller.Dependencies.Package)) {
            if ($d) { $d.Uri = "$feedUrl/" + ($d.Uri -split '/')[-1] }
        }
        $x.Save((Join-Path $feed 'NetFreeWidget.appinstaller'))
    }

    $msixName = (Get-ChildItem $feed -Filter 'NetFreeWidget_*.msix' | Select-Object -First 1).Name
    Write-Feed $Version $msixName

    # 1. First install through the installer (certificate + appinstaller).
    Remove-TestPackage
    $code = Invoke-Setup $setup $manifestUrl
    Check 'installer exits with 0' ($code -eq 0) "exit code $code"
    $pkg = Get-Package
    Check 'package is installed' ($null -ne $pkg)
    if (-not $pkg) { return }
    Check "installed version is $Version" ((Get-PackageVersion $pkg) -eq $Version) (Get-PackageVersion $pkg)
    Check 'package is signed (not a development registration)' (-not $pkg.IsDevelopmentMode -and "$($pkg.SignatureKind)" -ne 'None') "$($pkg.SignatureKind)"
    Check 'automatic updates are registered to the feed' ((Get-UpdateUri $pkg) -eq $manifestUrl) (Get-UpdateUri $pkg)
    $cer = Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Subject -eq $Publisher
    Check 'signing certificate is trusted' ($null -ne $cer)

    $avail = Get-UpdateAvailability $pkg
    Check 'no update reported while the feed has the same version' ($avail -like 'NoUpdates*') $avail

    # 2. Running the installer again is harmless.
    $code = Invoke-Setup $setup $manifestUrl
    Check 'installer re-run exits with 0' ($code -eq 0) "exit code $code"
    Check 'installer re-run keeps the version' ((Get-PackageVersion (Get-Package)) -eq $Version)

    # 2b. The fallback for networks where App Installer cannot download (NetFree): the installer downloads
    #     the files itself and installs from a local .appinstaller, still registered to the feed.
    Remove-TestPackage
    $code = Invoke-Setup $setup $manifestUrl -Fallback
    Check 'fallback install exits with 0' ($code -eq 0) "exit code $code"
    Check 'fallback install used the downloaded files' (($script:SetupLog -join "`n") -like '*installed from downloaded files*')
    Check 'fallback install did not need the last resort' (($script:SetupLog -join "`n") -notlike '*local .appinstaller:*')
    $pkg = Get-Package
    Check "fallback install is version $Version" ((Get-PackageVersion $pkg) -eq $Version) (Get-PackageVersion $pkg)
    Check 'fallback install is registered to the feed' ((Get-UpdateUri $pkg) -eq $manifestUrl) (Get-UpdateUri $pkg)

    # 3. A newer version appears in the feed, signed with the same certificate.
    $v = [Version]$Version
    $next = "$($v.Major).$($v.Minor).$($v.Build).$($v.Revision + 1)"
    $sdkBin = Split-Path (Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter makeappx.exe |
        Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1).FullName
    $unpacked = Join-Path $work 'unpacked'
    & "$sdkBin\makeappx.exe" unpack /p (Join-Path $feed $msixName) /d $unpacked /o | Out-Null
    Check 'makeappx unpack' ($LASTEXITCODE -eq 0) "exit $LASTEXITCODE"
    # Footprint files are regenerated by pack (and the signature by signtool).
    foreach ($f in 'AppxSignature.p7x', 'AppxBlockMap.xml', '[Content_Types].xml', 'AppxMetadata') {
        Remove-Item -LiteralPath (Join-Path $unpacked $f) -Recurse -Force -ErrorAction SilentlyContinue
    }
    $manifestPath = Join-Path $unpacked 'AppxManifest.xml'
    $m = Get-Content $manifestPath -Raw -Encoding utf8
    $m = ([regex]'(<Identity\b[^>]*?\bVersion=")[^"]*(")').Replace($m, "`${1}$next`${2}", 1)
    [IO.File]::WriteAllText($manifestPath, $m, (New-Object Text.UTF8Encoding($false)))
    $nextName = $msixName -replace [regex]::Escape($Version), $next
    & "$sdkBin\makeappx.exe" pack /d $unpacked /p (Join-Path $feed $nextName) /o | Out-Null
    Check 'makeappx pack' ($LASTEXITCODE -eq 0) "exit $LASTEXITCODE"
    & "$sdkBin\signtool.exe" sign /fd SHA256 /f $Pfx /p $PfxPassword (Join-Path $feed $nextName) | Out-Null
    Check "fake update $next packed and signed" ($LASTEXITCODE -eq 0) "signtool exit $LASTEXITCODE"
    Write-Feed $next $nextName

    $avail = Get-UpdateAvailability (Get-Package)
    Check 'Windows sees the new version as an available update' ($avail -like 'Available*' -or $avail -like 'Required*') $avail

    # The widget's own updater, which does not depend on App Installer's download.
    $updater = Get-Updater (Join-Path $feed $msixName)
    $code = Invoke-Updater $updater $manifestUrl
    Check 'widget updater exits with 0 (updated)' ($code -eq 0) "exit code $code"
    $pkg = Get-Package
    Check "updated in place to $next" ((Get-PackageVersion $pkg) -eq $next) (Get-PackageVersion $pkg)
    Check 'still registered for automatic updates after the update' ((Get-UpdateUri $pkg) -eq $manifestUrl) (Get-UpdateUri $pkg)
    Check 'no update reported after updating' ((Get-UpdateAvailability $pkg) -like 'NoUpdates*')
    $code = Invoke-Updater $updater $manifestUrl
    Check 'widget updater reports up to date (10)' ($code -eq 10) "exit code $code"

    Remove-TestPackage
}

# ---------- Remote ----------

function Invoke-RemoteTests {
    $latest = "https://github.com/$Repository/releases/latest/download"
    $tag = 'v' + ($Version -replace '\.0$', '')
    $manifestUrl = "$latest/NetFreeWidget.appinstaller"

    $xml = Get-Xml $manifestUrl
    Check "latest/download serves version $Version" ($xml.AppInstaller.Version -eq $Version) $xml.AppInstaller.Version
    Check 'main package points at this release' ($xml.AppInstaller.MainPackage.Uri -like "*/download/$tag/*") $xml.AppInstaller.MainPackage.Uri
    Check 'main package is downloadable' (Test-Url $xml.AppInstaller.MainPackage.Uri) $xml.AppInstaller.MainPackage.Uri
    foreach ($d in @($xml.AppInstaller.Dependencies.Package)) {
        if ($d) { Check "dependency $($d.Name) is downloadable" (Test-Url $d.Uri) $d.Uri }
    }

    $setup = Join-Path $work 'NetFreeWidget-Setup.exe'
    Invoke-WebRequest "$latest/NetFreeWidget-Setup.exe" -OutFile $setup -UseBasicParsing
    $sig = Get-AuthenticodeSignature $setup
    Check 'installer is signed with the publisher certificate' ($sig.SignerCertificate.Subject -eq $Publisher) "$($sig.SignerCertificate.Subject)"

    # Upgrade from the previous release, when it was signed with this same certificate.
    $previous = $null
    $releases = gh api "repos/$Repository/releases?per_page=20" | ConvertFrom-Json
    foreach ($r in $releases | Where-Object { $_.tag_name -ne $tag -and -not $_.draft }) {
        $asset = $r.assets | Where-Object name -eq 'NetFreeWidget.appinstaller'
        if (-not $asset) { continue }
        $prev = Get-Xml $asset.browser_download_url
        if ($prev.AppInstaller.MainPackage.Publisher -eq $Publisher) {
            $previous = @{ Url = $asset.browser_download_url; Version = $prev.AppInstaller.Version }
            break
        }
    }

    Remove-TestPackage
    if ($previous) {
        $code = Invoke-Setup $setup $previous.Url
        Check "previous release $($previous.Version) installs" ($code -eq 0) "exit code $code"
        $pkg = Get-Package
        if ($pkg) {
            Check 'previous release is registered to latest/download' ((Get-UpdateUri $pkg) -eq $manifestUrl) (Get-UpdateUri $pkg)
            # Informational only: the previous release was installed from its tag URL, not from latest/download
            # as a real user's was, and Windows then answers NoUpdates (seen on the 1.0.5 -> 1.0.6 run). The
            # update below, through the stored URI, is what proves the upgrade path; the Local tests cover
            # the availability check on a feed that matches the install source.
            Report INFO "update availability of the previous release: $(Get-UpdateAvailability $pkg)"
            # The widget's own updater over the real HTTPS URLs (the path that works on NetFree).
            $msix = Join-Path $work (Split-Path $xml.AppInstaller.MainPackage.Uri -Leaf)
            Invoke-WebRequest $xml.AppInstaller.MainPackage.Uri -OutFile $msix -UseBasicParsing
            $code = Invoke-Updater (Get-Updater $msix) $manifestUrl
            Check 'widget updater exits with 0 (updated)' ($code -eq 0) "exit code $code"
            $pkg = Get-Package
            Check "previous release updates to $Version" ((Get-PackageVersion $pkg) -eq $Version) (Get-PackageVersion $pkg)
            Check 'still registered to latest/download after the update' ((Get-UpdateUri $pkg) -eq $manifestUrl) (Get-UpdateUri $pkg)
        }
        Remove-TestPackage
    }
    else {
        Report SKIP 'upgrade test: no earlier release signed with this certificate yet'
    }

    # Fresh install exactly as a user does it: the downloaded installer with its built-in address.
    $code = Invoke-Setup $setup $null
    Check 'fresh install from GitHub exits with 0' ($code -eq 0) "exit code $code"
    $pkg = Get-Package
    Check "fresh install is version $Version" ((Get-PackageVersion $pkg) -eq $Version) (Get-PackageVersion $pkg)
    if ($pkg) {
        Check 'fresh install is registered to latest/download' ((Get-UpdateUri $pkg) -eq $manifestUrl) (Get-UpdateUri $pkg)
        Check 'no update reported for the latest version' ((Get-UpdateAvailability $pkg) -like 'NoUpdates*')
    }
    Remove-TestPackage
}

try {
    if ($Mode -eq 'Local') { Invoke-LocalTests } else { Invoke-RemoteTests }
}
catch {
    $script:Failures++
    Report FAIL "unexpected error: $($_.Exception.Message)"
    Write-Host $_.ScriptStackTrace
}

if ($script:Failures -gt 0) {
    Write-Host "$($script:Failures) check(s) failed." -ForegroundColor Red
    exit 1
}
Write-Host 'All checks passed.' -ForegroundColor Green
