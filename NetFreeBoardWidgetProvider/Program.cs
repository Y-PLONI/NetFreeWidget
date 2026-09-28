using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace NetFreeBoardWidgetProvider
{
    class Program
    {
        [DllImport("ole32.dll")]
        static extern int CoRegisterClassObject(
            [MarshalAs(UnmanagedType.LPStruct)] Guid rclsid,
            [MarshalAs(UnmanagedType.IUnknown)] object pUnk,
            uint dwClsContext,
            uint flags,
            out uint lpdwRegister);

        [DllImport("ole32.dll")]
        static extern int CoRevokeClassObject(uint dwRegister);

        [DllImport("ole32.dll")]
        static extern int CoResumeClassObjects();

        const uint CLSCTX_LOCAL_SERVER = 0x4;
        const uint REGCLS_MULTIPLEUSE = 0x1;
        const uint REGCLS_SUSPENDED = 0x4;
        const uint S_OK = 0;

        static uint _cookie = 0;

        static void Main(string[] args)
        {
            System.Console.WriteLine("Began Main execution!!!");
            try 
            {
                var widgetProviderFactory = new WidgetProviderFactory();
                Guid clsid = new Guid(WidgetProvider.WidgetProviderClassId);

                int hr = CoRegisterClassObject(
                    clsid,
                    widgetProviderFactory,
                    CLSCTX_LOCAL_SERVER,
                    REGCLS_MULTIPLEUSE | REGCLS_SUSPENDED,
                    out _cookie);

                if (hr == S_OK)
                {
                    CoResumeClassObjects();
                    var resetEvent = new ManualResetEvent(false);
                    var thread = new Thread(() =>
                    {
                        resetEvent.WaitOne();
                    });
                    thread.Start();

                    resetEvent.WaitOne();
                }
                else
                {
                    Log($"CoRegisterClassObject failed with HR: {hr}");
                }
            }
            catch (Exception ex)
            {
                Log($"Main Error: {ex}");
            }
        }

        static void Log(string message) 
        {
            try {
                string logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NetFreeWidget", "error_log.txt");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Program: {message}\n");
            } catch {}
        }
    }
}
