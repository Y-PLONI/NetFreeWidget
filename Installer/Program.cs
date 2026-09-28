using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Windows.Forms;

namespace WidgetSetup
{
    internal static class Program
    {
        /// <summary>Argument of the elevated child that only trusts the certificate.</summary>
        internal const string TrustCertArg = "--trust-cert";

        // Exit codes of the silent mode.
        private const int ExitOk = 0, ExitError = 1, ExitCertDeclined = 2, ExitCertFailed = 3, ExitInstallFailed = 4;

        /// <summary>
        /// Usage: NetFreeWidget-Setup.exe [--quiet] [--appinstaller &lt;uri&gt;] [--log &lt;file&gt;]
        ///   --quiet         no window; the result is the exit code (and the log file, if given)
        ///   --appinstaller  install from another .appinstaller (tests, mirrors) instead of the one in setup.ini
        /// </summary>
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 1 && args[0] == TrustCertArg)
                return SetupSteps.TrustInProcess();

            var config = Config.Load();
            bool quiet = false;
            string? log = null;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "--quiet": quiet = true; break;
                    case "--appinstaller" when i + 1 < args.Length: config.AppInstallerUri = args[++i]; break;
                    case "--log" when i + 1 < args.Length: log = args[++i]; break;
                }
            }

            if (quiet)
                return RunQuiet(config, log);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm(config));
            return ExitOk;
        }

        private static int RunQuiet(Config config, string? log)
        {
            void Log(string line)
            {
                if (log != null)
                    File.AppendAllText(log, line + Environment.NewLine, Encoding.UTF8);
            }

            try
            {
                var trust = SetupSteps.EnsureCertificateTrusted();
                Log($"certificate: {trust}");
                if (trust == SetupSteps.TrustResult.Declined)
                    return ExitCertDeclined;
                if (trust == SetupSteps.TrustResult.Failed)
                    return ExitCertFailed;

                Log($"installing from {config.AppInstallerUri}");
                var (ok, output) = SetupSteps.InstallPackage(config.AppInstallerUri);
                Log(ok ? "installed" : "install failed: " + output);
                return ok ? ExitOk : ExitInstallFailed;
            }
            catch (Exception ex)
            {
                Log("error: " + ex);
                return ExitError;
            }
        }
    }

    internal sealed class Config
    {
        public string Name = "";
        public string PackageName = "";
        public string AppInstallerUri = "";
        public string Done = "";

        public static Config Load()
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var reader = new StreamReader(Resource("setup.ini"), Encoding.UTF8))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    int eq = line.IndexOf('=');
                    if (line.Length == 0 || line[0] == '#' || eq <= 0)
                        continue;
                    values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }

            string Get(string key) => values.TryGetValue(key, out var v) ? v : "";
            return new Config
            {
                Name = Get("Name"),
                PackageName = Get("PackageName"),
                AppInstallerUri = Get("AppInstallerUri"),
                Done = Get("Done"),
            };
        }

        public static X509Certificate2 LoadCertificate()
        {
            using var stream = Resource("signing.cer");
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return new X509Certificate2(buffer.ToArray());
        }

        private static Stream Resource(string name) =>
            Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing embedded resource '{name}'.");
    }
}
