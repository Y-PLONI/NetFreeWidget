using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;

namespace WidgetSetup
{
    /// <summary>The two install steps, shared by the window and the silent mode.</summary>
    internal static class SetupSteps
    {
        /// <summary>Result of <see cref="EnsureCertificateTrusted"/>.</summary>
        internal enum TrustResult { AlreadyTrusted, Trusted, Declined, Failed }

        public static TrustResult EnsureCertificateTrusted()
        {
            var cert = Config.LoadCertificate();
            if (IsTrusted(cert))
                return TrustResult.AlreadyTrusted;

            int code = IsElevated() ? TrustInProcess() : TrustElevated();
            if (code == -1)
                return TrustResult.Declined;
            return code == 0 && IsTrusted(cert) ? TrustResult.Trusted : TrustResult.Failed;
        }

        /// <summary>Adds the embedded certificate to LocalMachine\TrustedPeople; needs elevation.</summary>
        public static int TrustInProcess()
        {
            try
            {
                using var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadWrite);
                store.Add(Config.LoadCertificate());
                return 0;
            }
            catch
            {
                return 1;
            }
        }

        /// <summary>
        /// Installs from the .appinstaller URI through the Appx module that ships with Windows. Installing this
        /// way (not from the .msix directly) is what makes Windows check that URI for updates later on.
        /// When App Installer cannot download the packages itself, falls back to downloading them here and
        /// installing them locally; <c>viaAppInstaller</c> is then false. See install-package.ps1.
        /// <paramref name="forceFallback"/> skips the first attempt (tests).
        /// </summary>
        public static (bool ok, bool viaAppInstaller, string output) InstallPackage(string appInstallerUri, bool forceFallback = false)
        {
            string uri = appInstallerUri.Replace("'", "''");
            string script;
            using (var reader = new StreamReader(Config.Resource("install-package.ps1"), Encoding.UTF8))
                script = $"$AppInstallerUri = '{uri}'\n$ForceFallback = ${(forceFallback ? "true" : "false")}\n" + reader.ReadToEnd();

            var psi = new ProcessStartInfo(
                Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"),
                "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " +
                Convert.ToBase64String(Encoding.Unicode.GetBytes(script)))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
            };

            using var process = Process.Start(psi)!;
            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return (process.ExitCode is 0 or 2, process.ExitCode == 0, output);
        }

        private static bool IsTrusted(X509Certificate2 cert)
        {
            using var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            return store.Certificates.Find(X509FindType.FindByThumbprint, cert.Thumbprint, validOnly: false).Count > 0;
        }

        private static bool IsElevated() =>
            new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

        /// <summary>Relaunches this exe elevated to trust the certificate; -1 when the user declined the UAC prompt.</summary>
        private static int TrustElevated()
        {
            var psi = new ProcessStartInfo(Application.ExecutablePath, Program.TrustCertArg)
            {
                UseShellExecute = true,
                Verb = "runas",
            };
            try
            {
                using var process = Process.Start(psi)!;
                process.WaitForExit();
                return process.ExitCode;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
            {
                return -1;
            }
        }
    }
}
