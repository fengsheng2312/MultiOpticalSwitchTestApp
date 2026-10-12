using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MultiOpticalSwitchTestApp.SoftwareUpdate
{
    /// <summary>SMB 共享访问探测结果</summary>
    public enum SmbProbeResult
    {
        /// <summary>当前登录账号可访问共享（有权限）</summary>
        Granted,

        /// <summary>当前登录账号无权限（访问被拒绝）</summary>
        Denied,

        /// <summary>网络不通 / 共享不存在（无法判定权限，按不可用处理）</summary>
        NetworkError
    }

    /// <summary>
    /// 账号级 SMB 准入探测：用当前 Windows 登录账号只读探测共享目录，判定该账号是否有访问权限。
    /// </summary>
    public static class SmbProbe
    {
        /// <summary>共享根路径（与 BurnWorker3 / SampleUpdaterAppNet10 / SoftwareUpdater472 保持一致）</summary>
        public const string DefaultSharePath = @"\\192.168.0.150\研发部文件共享\软件组";

        /// <summary>探测超时（毫秒）：UNC 访问不通时默认可能卡 10~30 秒，必须限制</summary>
        public const int DefaultTimeoutMs = 5000;

        /// <summary>探测当前登录账号能否访问共享目录</summary>
        public static async Task<SmbProbeResult> ProbeAsync(string? sharePath = null, int timeoutMs = DefaultTimeoutMs)
        {
            var path = string.IsNullOrWhiteSpace(sharePath) ? DefaultSharePath : sharePath.Trim();
            if (string.IsNullOrWhiteSpace(path)) return SmbProbeResult.NetworkError;

            // 放线程池执行，避免阻塞 UI 线程
            var task = Task.Run(() =>
            {
                try
                {
                    // 只读探测：列出共享下第一级目录。能列出即代表当前账号对该共享有读取权限。
                    Directory.GetDirectories(path);
                    return SmbProbeResult.Granted;
                }
                catch (UnauthorizedAccessException)
                {
                    return SmbProbeResult.Denied;
                }
                catch (Exception)
                {
                    return SmbProbeResult.NetworkError;
                }
            });

            // 超时保护：超时后按网络错误返回（后台探测任务继续执行无害，不等待）
            var done = await Task.WhenAny(task, Task.Delay(timeoutMs)).ConfigureAwait(false);
            if (done != task)
            {
                return SmbProbeResult.NetworkError;
            }
            return await task.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// SMB 共享仓库客户端：以当前 Windows 登录账号直接访问公司共享目录获取软件更新包，
    /// 完全替代服务器 HTTP 中转。目录约定：
    ///   {ShareRoot}\SoftwareRepository\{产品名}\{产品名}_v版本.zip
    /// </summary>
    public class ShareRepositoryClient : IDisposable
    {
        public string ShareRoot { get; }
        public string SoftwareRoot { get; }
        public string CacheDirectory { get; }

        public ShareRepositoryClient(string shareRoot)
        {
            ShareRoot = (shareRoot ?? "").Trim().TrimEnd('\\', '/');
            SoftwareRoot = Path.Combine(ShareRoot, "SoftwareRepository");

            // 本地缓存目录：%LOCALAPPDATA%\MultiOpticalSwitchTestApp\Cache（按 SHA256 命中避免重复读共享）
            CacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MultiOpticalSwitchTestApp", "Cache");
            Directory.CreateDirectory(CacheDirectory);
        }

        /// <summary>健康检查 = 当前账号能否读取共享根目录（3 秒超时，供快速判定）</summary>
        public async Task<bool> HealthAsync()
            => await SmbProbe.ProbeAsync(ShareRoot, 3000) == SmbProbeResult.Granted;

        /// <summary>获取指定软件的最新版本信息（扫描产品子目录下的 .zip/.exe，取版本号最高者），不存在返回 null</summary>
        public async Task<SoftwarePackageInfo?> GetLatestSoftwareAsync(string productName)
        {
            var versions = await GetSoftwareVersionsAsync(productName);
            return versions.Count == 0 ? null : versions[0];
        }

        /// <summary>获取指定软件的所有版本列表（扫描该产品子目录下的 .zip/.exe，按版本号降序）</summary>
        public async Task<List<SoftwarePackageInfo>> GetSoftwareVersionsAsync(string productName)
        {
            var result = new List<SoftwarePackageInfo>();
            var productDir = Path.Combine(SoftwareRoot, productName);
            if (!Directory.Exists(productDir)) return result;

            foreach (var file in Directory.GetFiles(productDir, "*.*", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(file);
                bool isPackage = name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                              || name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
                if (!isPackage) continue;

                var fi = new FileInfo(file);
                // 相对路径：产品名/文件名（下载时按此定位共享文件）
                var rel = productName + "/" + name;
                result.Add(new SoftwarePackageInfo
                {
                    ProductName = productName,
                    LatestVersion = FileNameParser.ExtractVersion(name),
                    FileName = rel,
                    Size = fi.Length,
                    Sha256 = ComputeSha256File(file),
                    Released = fi.LastWriteTime,
                    ForceUpdate = false,
                    MinSupportedVersion = "",
                    ReleaseNotes = "directory-scan"
                });
            }

            return result
                .OrderByDescending(p => p.LatestVersion, new SoftwareVersionComparer())
                .ToList();
        }

        /// <summary>下载软件更新包到本地缓存（带 SHA256 校验与进度）；优先读本地缓存，按 SHA256 命中</summary>
        public async Task<string> DownloadSoftwareAsync(SoftwarePackageInfo pkg, IProgress<double>? progress = null)
        {
            // 1. 缓存命中：本地已有相同 SHA256 的文件，直接用
            if (!string.IsNullOrEmpty(pkg.Sha256))
            {
                var cachedPath = GetCachedPath(pkg.Sha256, pkg.FileName);
                if (File.Exists(cachedPath))
                {
                    progress?.Report(1.0);
                    return cachedPath;
                }
            }

            // 2. 从共享复制到本地缓存（防路径穿越）
            var sourcePath = SafeCombine(SoftwareRoot, pkg.FileName);
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("更新包不存在：" + pkg.FileName);

            var cacheFile = GetCachedPath(pkg.Sha256 ?? "unknown", pkg.FileName);
            var tempFile = cacheFile + ".tmp";
            if (File.Exists(tempFile)) File.Delete(tempFile);

            var total = new FileInfo(sourcePath).Length;
            using (var src = File.OpenRead(sourcePath))
            using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                var buffer = new byte[65536];
                long readTotal = 0;
                int read;
                while ((read = await src.ReadAsync(buffer)) > 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, read));
                    readTotal += read;
                    if (total > 0) progress?.Report((double)readTotal / total);
                }
                fs.Flush();

                // 校验复制大小
                if (total > 0 && readTotal != total)
                {
                    fs.Close();
                    File.Delete(tempFile);
                    throw new IOException("复制不完整：期望 " + total + " 字节，实际 " + readTotal + " 字节");
                }
            }

            // 3. SHA256 校验
            if (!string.IsNullOrEmpty(pkg.Sha256))
            {
                var fileHash = ComputeSha256File(tempFile);
                if (!fileHash.Equals(pkg.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(tempFile);
                    throw new Exception("更新包 SHA256 校验失败：期望 " + pkg.Sha256.Substring(0, Math.Min(16, pkg.Sha256.Length)) + "…，实际 " + fileHash.Substring(0, 16) + "…");
                }
            }

            // 4. 校验通过，重命名为正式文件名
            if (File.Exists(cacheFile)) File.Delete(cacheFile);
            File.Move(tempFile, cacheFile);

            return cacheFile;
        }

        public void Dispose() { /* 无原生资源需释放 */ }

        // ============================================================
        // 工具方法
        // ============================================================

        /// <summary>计算文件 SHA256</summary>
        public static string ComputeSha256File(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            var hash = SHA256.HashData(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>缓存文件路径：{CacheDirectory}\{SHA256前8位}_{纯文件名}</summary>
        private string GetCachedPath(string sha256, string fileName)
        {
            var prefix = sha256.Length > 8 ? sha256.Substring(0, 8) : sha256;
            var safeName = fileName.Replace('/', '_');
            return Path.Combine(CacheDirectory, prefix + "_" + Path.GetFileName(safeName));
        }

        /// <summary>安全拼接路径，防止 ../ 穿越到仓库目录之外</summary>
        private static string SafeCombine(string rootPath, string relativePath)
        {
            var full = Path.GetFullPath(Path.Combine(rootPath, relativePath));
            var rootFull = Path.GetFullPath(rootPath);
            if (!full.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
                !full.Equals(rootFull, StringComparison.Ordinal))
                throw new UnauthorizedAccessException("路径越界");
            return full;
        }

        /// <summary>语义化版本比较：a &gt; b 返回 1，a == b 返回 0，a &lt; b 返回 -1</summary>
        private static int CompareVersion(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) a = "0.0.0";
            if (string.IsNullOrEmpty(b)) b = "0.0.0";
            a = a.TrimStart('v', 'V');
            b = b.TrimStart('v', 'V');
            var pa = a.Split('.');
            var pb = b.Split('.');
            for (int i = 0; i < Math.Max(pa.Length, pb.Length); i++)
            {
                int va = i < pa.Length && int.TryParse(pa[i], out var x) ? x : 0;
                int vb = i < pb.Length && int.TryParse(pb[i], out var y) ? y : 0;
                if (va != vb) return va.CompareTo(vb);
            }
            return 0;
        }

        /// <summary>软件版本排序比较器（按版本号降序）</summary>
        private sealed class SoftwareVersionComparer : IComparer<string>
        {
            public int Compare(string? a, string? b)
            {
                if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 0;
                if (string.IsNullOrEmpty(a)) return -1;
                if (string.IsNullOrEmpty(b)) return 1;
                return CompareVersion(a, b);
            }
        }
    }

    /// <summary>文件名解析（与 HexServer/BurnWorker3 目录约定保持一致）</summary>
    public static class FileNameParser
    {
        /// <summary>从文件名提取版本号（如 "环形器自动测试_v1.0.5.zip" → "1.0.5"）</summary>
        public static string ExtractVersion(string fileName)
        {
            var m = Regex.Match(fileName, "[vV]?(\\d+\\.\\d+\\.\\d+)");
            return m.Success ? m.Groups[1].Value : "0.0.0";
        }
    }
}
