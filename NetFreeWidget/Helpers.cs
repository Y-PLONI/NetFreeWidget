using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace NetFreeWidget
{
    public static class DesktopIntegration
    {
        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SendMessageTimeout(IntPtr windowHandle, uint Msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string? className, string? windowTitle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        public static void AttachToDesktop(nint windowHandle)
        {
            try
            {
                IntPtr progman = FindWindow("Progman", "Program Manager");

                // Send message to Progman to spawn a WorkerW
                IntPtr result = IntPtr.Zero;
                SendMessageTimeout(progman, 0x052C, new IntPtr(0), IntPtr.Zero, 0, 1000, out result);

                IntPtr workerW = IntPtr.Zero;
                EnumWindows(new EnumWindowsProc((tophandle, topparamhandle) =>
                {
                    IntPtr p = FindWindowEx(tophandle, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (p != IntPtr.Zero)
                    {
                        workerW = FindWindowEx(IntPtr.Zero, tophandle, "WorkerW", null);
                    }
                    return true;
                }), IntPtr.Zero);

                // If WorkerW wasn't found (sometimes happens dynamically), use Progman
                if (workerW != IntPtr.Zero)
                {
                    SetParent(windowHandle, workerW);
                }
                else
                {
                    SetParent(windowHandle, progman);
                }
            }
            catch { }
        }
    }

    public static class ThemeHelper
    {
        public static bool IsDarkMode()
        {
            try
            {
                // Check Windows system theme setting precisely
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key != null)
                {
                    // "AppsUseLightTheme" determines if apps should be light or dark by default.
                    // 0 = Dark Mode, 1 = Light Mode
                    object? registryValueObject = key.GetValue("AppsUseLightTheme");
                    if (registryValueObject != null)
                    {
                        int registryValue = (int)registryValueObject;
                        return registryValue == 0;
                    }
                }
            }
            catch { }
            return false; // Default to Light Mode if we can't read registry
        }
    }
}