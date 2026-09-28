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

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 1 && args[0] == TrustCertArg)
                return TrustCertificate();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm(Config.Load()));
            return 0;
        }

        /// <summary>Runs elevated: adds the signing certificate to LocalMachine\TrustedPeople.</summary>
        private static int TrustCertificate()
        {
            try
            {
                var cert = Config.LoadCertificate();
                using var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadWrite);
                store.Add(cert);
                return 0;
            }
            catch
            {
                return 1;
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
