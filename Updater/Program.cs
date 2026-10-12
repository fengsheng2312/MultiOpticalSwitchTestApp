using System.Diagnostics;
using System.IO.Compression;

namespace Updater;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("用法: Updater.exe <更新包路径> <主程序exe路径>");
            return 1;
        }

        string updatePackagePath = args[0];
        string mainExePath = Path.GetFullPath(args[1]);
        string appDir = Path.GetDirectoryName(mainExePath) ?? Directory.GetCurrentDirectory();
        string logPath = Path.Combine(appDir, "updater.log");

        try
        {
            Log(logPath, $"=== 更新开始 ===");
            Log(logPath, $"更新包: {updatePackagePath}");
            Log(logPath, $"主程序: {mainExePath}");
            Log(logPath, $"应用目录: {appDir}");

            if (!File.Exists(updatePackagePath))
            {
                Log(logPath, $"错误：更新包不存在: {updatePackagePath}");
                return 2;
            }

            // 等待主程序退出（最多等 10 秒）
            Log(logPath, "等待主程序退出...");
            var mainProcName = Path.GetFileNameWithoutExtension(mainExePath);
            int waited = 0;
            while (waited < 10000)
            {
                var procs = Process.GetProcessesByName(mainProcName);
                if (procs.Length == 0) break;
                Thread.Sleep(500);
                waited += 500;
            }
            Log(logPath, $"等待完成，耗时 {waited}ms");

            // 备份当前主程序（失败不影响更新）
            try
            {
                if (File.Exists(mainExePath))
                {
                    var backupPath = mainExePath + ".bak";
                    if (File.Exists(backupPath)) File.Delete(backupPath);
                    File.Copy(mainExePath, backupPath);
                    Log(logPath, $"已备份主程序到: {backupPath}");
                }
            }
            catch (Exception ex)
            {
                Log(logPath, $"备份失败（不影响更新）: {ex.Message}");
            }

            // 根据更新包类型处理
            string ext = Path.GetExtension(updatePackagePath).ToLowerInvariant();
            if (ext == ".zip")
            {
                Log(logPath, "检测到 zip 更新包，开始解压...");
                ApplyZip(updatePackagePath, appDir, logPath);
            }
            else if (ext == ".exe")
            {
                Log(logPath, "检测到 exe 更新包，开始替换...");
                ApplyExe(updatePackagePath, mainExePath, logPath);
            }
            else
            {
                Log(logPath, $"错误：不支持的更新包格式: {ext}");
                return 3;
            }

            Log(logPath, "更新完成，重启主程序...");

            // 重启主程序
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = mainExePath,
                    WorkingDirectory = appDir,
                    UseShellExecute = true
                });
                Log(logPath, "主程序已启动");
            }
            catch (Exception ex)
            {
                Log(logPath, $"启动主程序失败: {ex.Message}");
            }

            Log(logPath, "=== 更新完成 ===");

            // 自删（延迟执行，确保日志写入完成）
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000);
                try
                {
                    var self = Process.GetCurrentProcess().MainModule?.FileName;
                    if (self is not null)
                    {
                        var psi = new ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 3 > nul & del /f /q \"" + self + "\"")
                        {
                            WindowStyle = ProcessWindowStyle.Hidden,
                            CreateNoWindow = true
                        };
                        Process.Start(psi);
                    }
                }
                catch { }
            });

            return 0;
        }
        catch (Exception ex)
        {
            Log(logPath, $"更新失败: {ex.GetType().Name}: {ex.Message}");
            Log(logPath, $"堆栈: {ex.StackTrace}");
            Console.WriteLine($"更新失败：{ex.Message}\n详细日志请查看: {logPath}");
            return 99;
        }
    }

    static void ApplyZip(string zipPath, string appDir, string logPath)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"update_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            ZipFile.ExtractToDirectory(zipPath, tempDir, overwriteFiles: true);
            Log(logPath, $"已解压到临时目录: {tempDir}");

            // 复制所有文件到应用目录，自动排除 Updater 目录
            var files = Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories);
            int copied = 0;
            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(tempDir, file);
                // 跳过 Updater 目录（避免覆盖正在运行的更新器）
                if (relativePath.StartsWith("Updater", StringComparison.OrdinalIgnoreCase))
                {
                    Log(logPath, $"跳过 Updater 文件: {relativePath}");
                    continue;
                }

                var destPath = Path.Combine(appDir, relativePath);
                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);

                // 重试机制（文件可能被占用）
                int retries = 0;
                while (retries < 5)
                {
                    try
                    {
                        File.Copy(file, destPath, overwrite: true);
                        copied++;
                        break;
                    }
                    catch (IOException) when (retries < 4)
                    {
                        retries++;
                        Thread.Sleep(500);
                    }
                }
            }
            Log(logPath, $"已复制 {copied} 个文件到应用目录");
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    static void ApplyExe(string exePath, string mainExePath, string logPath)
    {
        // exe 更新包直接替换主程序 exe
        int retries = 0;
        while (retries < 5)
        {
            try
            {
                File.Copy(exePath, mainExePath, overwrite: true);
                Log(logPath, $"已替换主程序 exe: {mainExePath}");
                return;
            }
            catch (IOException) when (retries < 4)
            {
                retries++;
                Thread.Sleep(500);
            }
        }
        throw new IOException("无法替换主程序 exe，文件可能被占用");
    }

    static void Log(string logPath, string msg)
    {
        try
        {
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n");
        }
        catch { }
    }
}
