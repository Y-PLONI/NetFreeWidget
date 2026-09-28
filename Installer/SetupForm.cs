using System;
using System.Drawing;
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
                SetStatus("מתקין את תעודת החתימה של הווידג'ט.\r\nאם Windows מבקש הרשאות, יש לאשר.");
                switch (await Task.Run(SetupSteps.EnsureCertificateTrusted))
                {
                    case SetupSteps.TrustResult.Declined:
                        Finish("ההתקנה בוטלה: בלי אישור מנהל אי אפשר להתקין את תעודת החתימה.", ok: false);
                        return;
                    case SetupSteps.TrustResult.Failed:
                        Finish("התקנת תעודת החתימה נכשלה.", ok: false);
                        return;
                }

                SetStatus("מוריד ומתקין את הווידג'ט...\r\nזה עשוי לקחת דקה או שתיים.");
                // Also after the fallback download: the widget then checks for updates and installs them itself.
                var (installed, _, output) = await Task.Run(() => SetupSteps.InstallPackage(_config.AppInstallerUri));
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
    }
}
