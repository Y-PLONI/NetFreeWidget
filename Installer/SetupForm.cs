using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WidgetSetup
{
    /// <summary>
    /// Two steps: trust the signing certificate (one UAC prompt, skipped when already trusted), then install
    /// the package from the .appinstaller URL as the signed-in user, which also registers automatic updates.
    /// </summary>
    internal sealed class SetupForm : Form
    {
        private readonly Config _config;
        private readonly TextBox _status;
        private readonly ProgressBar _progress;
        private readonly Button _close;

        public SetupForm(Config config)
        {
            _config = config;

            Text = $"התקנת {config.Name}";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 10f);
            ClientSize = new Size(480, 210);
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            var title = new Label
            {
                Text = config.Name,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 16),
            };

            _status = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = BackColor,
                TabStop = false,
                ScrollBars = ScrollBars.None,
                Location = new Point(20, 60),
                Size = new Size(440, 72),
            };

            _progress = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Location = new Point(20, 140),
                Size = new Size(440, 14),
            };

            _close = new Button
            {
                Text = "סגור",
                Enabled = false,
                Size = new Size(100, 32),
                Location = new Point(360, 166),
            };
            _close.Click += (_, _) => Close();

            Controls.AddRange(new Control[] { title, _status, _progress, _close });
            AcceptButton = _close;
            Shown += async (_, _) => await RunAsync();
        }

        private async Task RunAsync()
        {
            try
            {
                var cert = Config.LoadCertificate();
                if (!IsTrusted(cert))
                {
                    SetStatus("מתקין את תעודת החתימה של הווידג'ט.\r\nיש לאשר את בקשת ההרשאות של Windows.");
                    int code = await Task.Run(TrustElevated);
                    if (code == -1)
                    {
                        Finish("ההתקנה בוטלה: בלי אישור מנהל אי אפשר להתקין את תעודת החתימה.", ok: false);
                        return;
                    }
                    if (code != 0 || !IsTrusted(cert))
                    {
                        Finish("התקנת תעודת החתימה נכשלה.", ok: false);
                        return;
                    }
                }

                SetStatus("מוריד ומתקין את הווידג'ט...\r\nזה עשוי לקחת דקה או שתיים.");
                var (installed, output) = await Task.Run(InstallPackage);
                if (installed)
                    Finish($"ההתקנה הושלמה!\r\n{_config.Done}\r\nהווידג'ט יתעדכן מעצמו כשתצא גרסה חדשה.", ok: true);
                else
                    Finish("ההתקנה נכשלה:\r\n" + output, ok: false);
            }
            catch (Exception ex)
            {
                Finish("ההתקנה נכשלה:\r\n" + ex.Message, ok: false);
            }
        }

        private void SetStatus(string text) => _status.Text = text;

        private void Finish(string text, bool ok)
        {
            _status.ForeColor = ok ? ForeColor : Color.Firebrick;
            _status.ScrollBars = _status.GetLineFromCharIndex(text.Length) > 3 ? ScrollBars.Vertical : ScrollBars.None;
            _status.Text = text;
            _progress.Visible = false;
            _close.Enabled = true;
            _close.Focus();
        }

        private static bool IsTrusted(X509Certificate2 cert)
        {
            using var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
            return store.Certificates.Find(X509FindType.FindByThumbprint, cert.Thumbprint, validOnly: false).Count > 0;
        }

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

        /// <summary>Installs from the .appinstaller URL through the Appx module that ships with Windows.</summary>
        private (bool ok, string output) InstallPackage()
        {
            string uri = _config.AppInstallerUri.Replace("'", "''");
            string script =
                "$ProgressPreference = 'SilentlyContinue'; " +
                "[Console]::OutputEncoding = [Text.Encoding]::UTF8; " +
                $"try {{ Add-AppxPackage -Path '{uri}' -AppInstallerFile -ForceTargetApplicationShutdown -ErrorAction Stop; exit 0 }} " +
                "catch { Write-Output $_.Exception.Message; exit 1 }";

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
            return (process.ExitCode == 0, output);
        }
    }
}
