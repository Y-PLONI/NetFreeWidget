using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace NetFreeWidget
{
    public partial class MainWindow : Window
    {
        private Settings settings;
        private System.Windows.Threading.DispatcherTimer refreshTimer;

        public MainWindow()
        {
            InitializeComponent();
            
            ApplyTheme();

            Loaded += MainWindow_Loaded;

            settings = Settings.Load();

            // Load Settings UI
            if (DateTime.TryParse(settings.PackageStartDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
            {
                StartDateInput.Text = dt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            }
            else
            {
                StartDateInput.Text = settings.PackageStartDate; // show raw if failed
            }
            
            QuotaInput.Text = settings.PackageQuotaGb > 0 ? settings.PackageQuotaGb.ToString(CultureInfo.InvariantCulture) : "";
            WeekendCheckbox.IsChecked = settings.WeekendMode == "one";

            refreshTimer = new System.Windows.Threading.DispatcherTimer();
            refreshTimer.Interval = TimeSpan.FromMinutes(5);
            refreshTimer.Tick += async (s, e) => await RefreshData();
            refreshTimer.Start();

            _ = RefreshData();
        }

        private void ApplyTheme()
        {
            bool isDark = ThemeHelper.IsDarkMode();

            // Main Background
            MainBorder.Background = new SolidColorBrush(isDark ? Color.FromArgb(220, 26, 26, 26) : Color.FromArgb(220, 255, 255, 255));
            MainBorder.BorderBrush = new SolidColorBrush(isDark ? Color.FromArgb(51, 255, 255, 255) : Color.FromArgb(51, 0, 0, 0));
            
            // Text
            TitleText.Foreground = new SolidColorBrush(isDark ? Colors.White : Colors.Black);
            UsageText.Foreground = new SolidColorBrush(isDark ? Color.FromRgb(224, 224, 224) : Color.FromRgb(50, 50, 50));
            
            // App Buttons 
            BtnRefresh.Foreground = TitleText.Foreground;
            BtnSettings.Foreground = TitleText.Foreground;
            BtnClose.Foreground = TitleText.Foreground;

            // Settings Overlay
            SettingsView.Background = new SolidColorBrush(isDark ? Color.FromArgb(238, 26, 26, 26) : Color.FromArgb(238, 245, 245, 245));
            StartDateInput.Foreground = TitleText.Foreground;
            StartDateInput.Background = new SolidColorBrush(isDark ? Color.FromArgb(51, 255, 255, 255) : Color.FromArgb(25, 0, 0, 0));
            QuotaInput.Foreground = TitleText.Foreground;
            QuotaInput.Background = StartDateInput.Background;
            WeekendCheckbox.Foreground = TitleText.Foreground;
            
            // Progress Bar Track
            ProgressBg.Background = new SolidColorBrush(isDark ? Color.FromArgb(51, 255, 255, 255) : Color.FromArgb(30, 0, 0, 0));
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var desktopWorkingArea = SystemParameters.WorkArea;
            this.Left = desktopWorkingArea.Right - this.Width - 30;
            this.Top = desktopWorkingArea.Bottom - this.Height - 30;

            // To avoid the black box rendering bug in modern Windows when attaching direct WPF surfaces 
            // to WorkerW (which handles the desktop icon list), we're going to keep it a standard window,
            // but lock it to the absolute bottom of the Z-Order, completely underneath everything else!
            var hWnd = new WindowInteropHelper(this).Handle;
            
            // Set ToolWindow style so it doesn't appear in Alt+Tab
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            SetWindowLong(hWnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);

            // Send to very bottom
            SetWindowPos(hWnd, new IntPtr(HWND_BOTTOM), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        // --- Win32 definitions to lock the window to the desktop natively without destroying WPF render context ---
        
        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        
        [DllImport("user32.dll", SetLastError = true)]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        
        private const int HWND_BOTTOM = 1;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const int WM_WINDOWPOSCHANGING = 0x0046;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var source = PresentationSource.FromVisual(this) as HwndSource;
            source?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Forces the window to stay at the bottom of the Z-Order (behind all apps)
            if (msg == WM_WINDOWPOSCHANGING)
            {
                var windowPos = (WINDOWPOS)Marshal.PtrToStructure(lParam, typeof(WINDOWPOS))!;
                windowPos.hwndInsertAfter = new IntPtr(HWND_BOTTOM);
                windowPos.flags &= ~(uint)0x0004; // Remove SWP_NOZORDER flag
                Marshal.StructureToPtr(windowPos, lParam, true);
            }
            return IntPtr.Zero;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public uint flags;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            SettingsView.Visibility = SettingsView.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            await RefreshData();
        }

        private void UpdateStatusVisuals(bool isError, bool isWarning, bool isSuccess)
        {
            if (isError)
            {
                ProgressBar.Background = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
                StatusBox.Background = new SolidColorBrush(Color.FromArgb(50, 244, 67, 54));
                StatusText1.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
            }
            else if (isWarning)
            {
                ProgressBar.Background = new SolidColorBrush(Color.FromRgb(96, 205, 255)); // Blue usually for warning/missing data
                StatusBox.Background = new SolidColorBrush(Color.FromArgb(50, 255, 183, 77)); // Orange
                StatusText1.Foreground = new SolidColorBrush(ThemeHelper.IsDarkMode() ? Color.FromRgb(255, 183, 77) : Color.FromRgb(230, 81, 0));
            }
            else if (isSuccess)
            {
                ProgressBar.Background = new SolidColorBrush(Color.FromRgb(96, 205, 255)); // Blue
                StatusBox.Background = new SolidColorBrush(Color.FromArgb(50, 76, 175, 80)); // Green
                StatusText1.Foreground = new SolidColorBrush(ThemeHelper.IsDarkMode() ? Color.FromRgb(129, 199, 132) : Color.FromRgb(46, 125, 50));
            }
        }

        private async System.Threading.Tasks.Task RefreshData()
        {
            UsageText.Text = "מחשב...";
            UsageText.Visibility = Visibility.Visible;
            StatsGrid.Visibility = Visibility.Collapsed;
            
            UpdateStatusVisuals(false, true, false);
            StatusText1.Text = "מקבל נתונים...";
            StatusText1.Inlines.Clear();

            double currentUsageGb = await NetFreeApi.GetUsageGb();
            
            if (double.IsNaN(currentUsageGb))
            {
                UsageText.Text = "לא זוהה משתמש";
                UpdateStatusVisuals(true, false, false);
                StatusText1.Text = "שגיאה בחיבור לנטפרי. רענן עם ↻.";
                return;
            }

            var dates = UsageCalculator.GetCycleDates(settings.PackageStartDate);
            
            if (settings.PackageQuotaGb <= 0 || dates == null)
            {
                UsageText.Text = $"נוצל: {currentUsageGb:F1} GB";
                ProgressBar.Width = 0;
                UpdateStatusVisuals(false, true, false);
                StatusText1.Text = "חסרים נתונים לחישוב! לחץ על גלגל השיניים להגדרות.";
                return;
            }

            UsageText.Visibility = Visibility.Collapsed;
            StatsGrid.Visibility = Visibility.Visible;
            TxtUsed.Text = $"GB {currentUsageGb:F2}";
            TxtTotal.Text = $"GB {settings.PackageQuotaGb:F1}";

            var (cycleStart, cycleEnd, today) = dates.Value;
            int totalUnits = UsageCalculator.CountUnits(cycleStart, cycleEnd, settings.WeekendMode);
            int elapsedUnits = UsageCalculator.CountUnits(cycleStart, today, settings.WeekendMode);

            double expectedGb = (settings.PackageQuotaGb / totalUnits) * elapsedUnits;
            TxtExpected.Text = $"GB {expectedGb:F2}";

            double usedPercent = Math.Min((currentUsageGb / settings.PackageQuotaGb) * 100, 100);
            
            double maxWidth = MainBorder.ActualWidth > 0 ? MainBorder.ActualWidth - 30 : 290; 
            ProgressBar.Width = (usedPercent / 100.0) * maxWidth;

            if (expectedGb > 0 && expectedGb < settings.PackageQuotaGb)
            {
                double expectedPercent = (expectedGb / settings.PackageQuotaGb) * 100;
                double expectedOffset = (expectedPercent / 100.0) * maxWidth;
                ExpectedMarker.Margin = new Thickness(0, -3, expectedOffset, -3);
                ExpectedMarker.Visibility = currentUsageGb > expectedGb ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                ExpectedMarker.Visibility = Visibility.Collapsed;
            }

            StatusText1.Inlines.Clear();
            if (currentUsageGb > expectedGb)
            {
                UpdateStatusVisuals(true, false, false);

                StatusText1.Inlines.Add(new System.Windows.Documents.Run("⚠️ חריגה: ") { FontWeight = FontWeights.Bold });
                StatusText1.Inlines.Add(new System.Windows.Documents.Run($"{(currentUsageGb - expectedGb):F2} GB מעל המומלץ.  "));

                if (elapsedUnits > 0)
                {
                    double ratePerUnit = currentUsageGb / elapsedUnits;
                    double remainingGb = settings.PackageQuotaGb - currentUsageGb;

                    if (remainingGb > 0)
                    {
                        var exhaustDate = UsageCalculator.PredictExhaustionDate(today, remainingGb, ratePerUnit, settings.WeekendMode);
                        if (exhaustDate.HasValue)
                        {
                            StatusText1.Inlines.Add(new System.Windows.Documents.Run("📉 תחזית: ") { FontWeight = FontWeights.Bold });
                            
                            string dayName = exhaustDate.Value.ToString("dddd", new CultureInfo("he-IL"));
                            string dateStr = exhaustDate.Value.ToString("dd/MM", CultureInfo.InvariantCulture);
                            StatusText1.Inlines.Add(new System.Windows.Documents.Run($"החבילה תגמר ב{dayName} ({dateStr})."));
                        }
                    }
                    else
                    {
                        StatusText1.Inlines.Add(new System.Windows.Documents.Run("📉 תחזית: ") { FontWeight = FontWeights.Bold });
                        StatusText1.Inlines.Add(new System.Windows.Documents.Run("החבילה הסתיימה לחלוטין!"));
                    }
                }
            }
            else
            {
                UpdateStatusVisuals(false, false, true);
                StatusText1.Inlines.Add(new System.Windows.Documents.Run("✅ הכל תקין: "));
                StatusText1.Inlines.Add(new System.Windows.Documents.Run($"נותרו {(expectedGb - currentUsageGb):F1} GB להיום."));
            }
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            // Parse Date
            if (DateTime.TryParseExact(StartDateInput.Text, new[] { "dd/MM/yyyy", "dd-MM-yyyy", "d/M/yyyy", "yyyy-MM-dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
            {
                settings.PackageStartDate = dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            else
            {
                settings.PackageStartDate = StartDateInput.Text;
            }

            // Parse Quota safely
            string cleanQuota = QuotaInput.Text.Replace(",", "."); // Handle Israeli commas 
            if (double.TryParse(cleanQuota, NumberStyles.Any, CultureInfo.InvariantCulture, out double quota))
            {
                settings.PackageQuotaGb = quota;
            }

            settings.WeekendMode = WeekendCheckbox.IsChecked == true ? "one" : "two";
            
            settings.Save(); // Call standard JSON file save

            SettingsView.Visibility = Visibility.Collapsed;
            _ = RefreshData();
        }
    }
}