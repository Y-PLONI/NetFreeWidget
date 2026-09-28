using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NetFreeWidget.Core;
using Windows.Management.Deployment;

namespace NetFreeBoardWidgetProvider
{
    /// <summary>
    /// Updates the widget without App Installer's own download. Windows checks the .appinstaller for updates
    /// through Delivery Optimization, which fails on some filtered networks (NetFree: 0x80D05011) where a plain
    /// HTTPS download works. So the provider downloads the .appinstaller and the packages itself, and installs
    /// them from a local copy of the .appinstaller whose Uri still points at the published one: the package
    /// stays registered for Windows' own updates, wherever those work.
    /// </summary>
    internal static class SelfUpdater
    {
        public const string DefaultFeed = "https://github.com/Y-PLONI/NetFreeWidget/releases/latest/download/NetFreeWidget.appinstaller";

        public enum Result { Updated, UpToDate, Failed }

        private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };
        private static long _lastCheckTicks;
        private static int _running;

        /// <summary>Starts a check in the background, at most once per <see cref="Interval"/> per process.</summary>
        public static void CheckInBackground()
        {
            long now = DateTime.UtcNow.Ticks;
            long last = Interlocked.Read(ref _lastCheckTicks);
            if (last != 0 && now - last < Interval.Ticks)
                return;
            if (IsDevelopmentBuild() || Interlocked.Exchange(ref _running, 1) == 1)
                return;
            Interlocked.Exchange(ref _lastCheckTicks, now);

            _ = Task.Run(async () =>
            {
                try { await RunAsync(null).ConfigureAwait(false); }
                finally { Interlocked.Exchange(ref _running, 0); }
            });
        }

        /// <summary>
        /// One check-and-update. <paramref name="feed"/> null: the .appinstaller the package is registered to,
        /// or <see cref="DefaultFeed"/>. Installing the update shuts this process down when it runs in the package.
        /// </summary>
        public static async Task<Result> RunAsync(string? feed)
        {
            string dir = Path.Combine(Path.GetTempPath(), "NetFreeWidget-update");
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
                Directory.CreateDirectory(dir);

                var pm = new PackageManager();
                feed ??= RegisteredFeed() ?? DefaultFeed;
                var feedUri = new Uri(feed);

                string manifestPath = Path.Combine(dir, "feed.appinstaller");
                await FetchAsync(feedUri, manifestPath).ConfigureAwait(false);
                var doc = XDocument.Load(manifestPath);
                var root = doc.Root!;
                var main = root.Elements().First(e => e.Name.LocalName == "MainPackage");

                string name = (string)main.Attribute("Name")!, publisher = (string)main.Attribute("Publisher")!;
                var available = Version.Parse((string)main.Attribute("Version")!);
                var installed = FindInstalled(pm, name, publisher);
                if (installed == null)
                {
                    Log.Error("SelfUpdater", $"{name} ({publisher}) is not installed; nothing to update");
                    return Result.Failed;
                }
                if (installed >= available)
                    return Result.UpToDate;

                // Only dependencies that are missing or older than required: passing one that is already installed
                // in a newer version fails with 0x80073D02.
                var dependencies = root.Elements().FirstOrDefault(e => e.Name.LocalName == "Dependencies");
                var depFiles = new System.Collections.Generic.List<Uri>();
                foreach (var dep in dependencies?.Elements().ToList() ?? new())
                {
                    var have = FindInstalled(pm, (string)dep.Attribute("Name")!, (string)dep.Attribute("Publisher")!,
                        (string?)dep.Attribute("ProcessorArchitecture"));
                    if (have != null && have >= Version.Parse((string)dep.Attribute("Version")!))
                    {
                        dep.Remove();
                        continue;
                    }
                    var local = await DownloadAsync(feedUri, (string)dep.Attribute("Uri")!, dir).ConfigureAwait(false);
                    dep.SetAttributeValue("Uri", local.AbsoluteUri);
                    depFiles.Add(local);
                }
                if (dependencies != null && !dependencies.HasElements)
                    dependencies.Remove();

                var mainFile = await DownloadAsync(feedUri, (string)main.Attribute("Uri")!, dir).ConfigureAwait(false);
                main.SetAttributeValue("Uri", mainFile.AbsoluteUri);

                // The root Uri stays the published address, so the update registration carries over.
                root.SetAttributeValue("Uri", feedUri.AbsoluteUri);
                string localManifest = Path.Combine(dir, "local.appinstaller");
                doc.Save(localManifest);

                Log.Info("SelfUpdater", $"updating {installed} -> {available} from {feedUri}");
                var result = await pm.AddPackageByAppInstallerFileAsync(new Uri(localManifest),
                    AddPackageByAppInstallerOptions.ForceTargetAppShutdown, pm.GetDefaultPackageVolume());
                if (result.ExtendedErrorCode == null)
                    return Result.Updated;

                // Last resort: the packages themselves, without the update registration.
                Log.Error("SelfUpdater", $"install from the local .appinstaller failed: {result.ErrorText}");
                result = await pm.AddPackageAsync(mainFile, depFiles, DeploymentOptions.ForceTargetApplicationShutdown);
                if (result.ExtendedErrorCode == null)
                    return Result.Updated;

                Log.Error("SelfUpdater", $"install failed: {result.ErrorText}");
                return Result.Failed;
            }
            catch (Exception ex)
            {
                Log.Error("SelfUpdater", ex);
                return Result.Failed;
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }

        /// <summary>A registration from build-and-deploy.ps1: never replace it with a published release.</summary>
        private static bool IsDevelopmentBuild()
        {
            try { return Windows.ApplicationModel.Package.Current.IsDevelopmentMode; }
            catch { return true; }
        }

        private static string? RegisteredFeed()
        {
            try
            {
                // Only inside the package; the command line mode passes the feed explicitly or uses the default.
                return Windows.ApplicationModel.Package.Current.GetAppInstallerInfo()?.Uri?.AbsoluteUri;
            }
            catch
            {
                return null;
            }
        }

        private static Version? FindInstalled(PackageManager pm, string name, string publisher, string? architecture = null)
        {
            Version? best = null;
            foreach (var p in pm.FindPackagesForUser("", name, publisher))
            {
                if (architecture != null && !string.Equals(p.Id.Architecture.ToString(), architecture, StringComparison.OrdinalIgnoreCase))
                    continue;
                var v = p.Id.Version;
                var version = new Version(v.Major, v.Minor, v.Build, v.Revision);
                if (best == null || version > best)
                    best = version;
            }
            return best;
        }

        private static async Task<Uri> DownloadAsync(Uri feed, string uri, string dir)
        {
            var source = new Uri(feed, uri);
            string file = Path.Combine(dir, Path.GetFileName(source.LocalPath));
            await FetchAsync(source, file).ConfigureAwait(false);
            return new Uri(file);
        }

        /// <summary>Plain HTTPS (redirects followed), or a file:// feed for tests.</summary>
        private static async Task FetchAsync(Uri source, string file)
        {
            if (source.IsFile)
            {
                File.Copy(source.LocalPath, file, overwrite: true);
                return;
            }
            using var response = await Http.GetAsync(source, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var output = File.Create(file);
            await response.Content.CopyToAsync(output).ConfigureAwait(false);
        }
    }
}
