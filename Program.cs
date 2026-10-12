using System.Runtime.InteropServices;
using System.Text;

namespace MultiOpticalSwitchTestApp
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();
        /// </summary>
        [STAThread]
        static void Main()
        {
            AllocConsole();
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Application.Run(new Form1());

            FreeConsole();
        }
    }
}