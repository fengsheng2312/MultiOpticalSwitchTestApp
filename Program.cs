using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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

        /// <summary>崩溃日志目录（程序目录下 Logs）。</summary>
        private static string CrashLogDir => Path.Combine(AppContext.BaseDirectory, "Logs");

        [STAThread]
        static void Main()
        {
            //AllocConsole();
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // 全局异常兜底：未处理异常写入 Logs\crash-*.log 且不终止进程，
            // 避免轮询等后台并发问题表现为“无提示闪退”、测试机上查不到原因
            Application.ThreadException += (_, e) =>
            {
                ReportUnhandled("UI线程", e.Exception);
                ShowCrashHint(e.Exception);
            };
            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            }
            catch (InvalidOperationException)
            {
                // 句柄已创建时无法更改异常模式，保持默认行为即可
            }
            // 后台线程的未处理异常：这里尽力记录，进程能否存活取决于运行时策略，
            // 但日志一定先落盘，便于事后定位
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                ReportUnhandled("非UI线程", e.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                ReportUnhandled("后台任务", e.Exception);
                e.SetObserved();
            };

            Application.Run(new Form1());

            FreeConsole();
        }

        /// <summary>把未处理异常写入
        /// 
        /// 
        /// 与崩溃日志文件。</summary>
        private static void ReportUnhandled(string source, Exception? ex)
        {
            if (ex is null) return;
            string text = $"[未处理异常:{source}] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}{Environment.NewLine}{ex}{Environment.NewLine}";
            Console.WriteLine(text);
            try
            {
                Directory.CreateDirectory(CrashLogDir);
                string file = Path.Combine(CrashLogDir, $"crash-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(file, text);
            }
            catch
            {
                // 日志写失败不能影响进程，忽略
            }
        }

        /// <summary>UI 线程异常时给出可见提示，避免用户以为程序无故卡住。</summary>
        private static void ShowCrashHint(Exception ex)
        {
            try
            {
                MessageBox.Show(
                    $"程序遇到未处理的异常，已记录到 Logs 目录：\n\n{ex.Message}",
                    "运行异常",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            catch
            {
                // 弹窗失败忽略
            }
        }
    }
}
