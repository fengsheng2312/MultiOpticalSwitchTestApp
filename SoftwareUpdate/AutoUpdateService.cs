using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MultiOpticalSwitchTestApp.SoftwareUpdate
{
    public class SoftwarePackageInfo
    {
        public string ProductName { get; set; } = "";
        public string LatestVersion { get; set; } = "";
        public string FileName { get; set; } = "";  // 相对路径，如 "环形器自动测试/环形器自动测试_v1.1.0.zip"
        public long Size { get; set; }
        public string Sha256 { get; set; } = "";
        public DateTime Released { get; set; }
        public bool ForceUpdate { get; set; }
        public string MinSupportedVersion { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
    }

    /// <summary>
    /// 上位机自更新服务（直连 SMB 共享目录模式，不再依赖服务器）
    /// 检查更新（扫描共享 SoftwareRepository\{产品名}\）→ 下载更新包（SHA256校验）→ 启动 updater.exe → 主程序退出
    /// updater 等待主程序退出后替换文件 → 重启主程序 → 自删
    /// 支持多个上位机软件各自管理版本（按 productName 区分）
    /// </summary>
    public class AutoUpdateService
    {
        private readonly string _shareRoot;
        public string CurrentVersion { get; }
        public string ProductName { get; }

        public AutoUpdateService(string shareRoot, string currentVersion, string productName)
        {
            _shareRoot = (shareRoot ?? "").Trim();
            CurrentVersion = currentVersion;
            ProductName = productName;
        }

        /// <summary>检查更新，有新版返回 SoftwarePackageInfo，无更新返回 null；无权限/网络不通抛异常</summary>
        public async Task<SoftwarePackageInfo?> CheckUpdateAsync()
        {
            // 先探测共享访问权限：无权限/网络不通给出明确原因
            var probe = await SmbProbe.ProbeAsync(_shareRoot);
            if (probe == SmbProbeResult.Denied)
                throw new Exception("当前账号对该共享目录无访问权限，无法检查更新");
            if (probe == SmbProbeResult.NetworkError)
                throw new Exception("网络不通或共享目录不存在，无法检查更新：" + _shareRoot);

            using var repo = new ShareRepositoryClient(_shareRoot);
            var pkg = await repo.GetLatestSoftwareAsync(ProductName);
            if (pkg is null) return null;

            // 版本比较：共享目录版本 > 当前版本才提示更新
            if (CompareVersion(pkg.LatestVersion, CurrentVersion) > 0)
                return pkg;
            return null;
        }

        /// <summary>下载更新包到本地缓存，返回本地路径；SHA256 校验失败抛异常</summary>
        public async Task<string> DownloadUpdateAsync(SoftwarePackageInfo pkg, IProgress<double>? progress = null)
        {
            using var repo = new ShareRepositoryClient(_shareRoot);
            return await repo.DownloadSoftwareAsync(pkg, progress);
        }

        /// <summary>启动 updater.exe 并退出主程序（updater 会替换文件后重启）</summary>
        public void ApplyUpdate(string updatePackagePath)
        {
            var updaterPath = Path.Combine(AppContext.BaseDirectory, "Updater", "Updater.exe");
            if (!File.Exists(updaterPath))
                throw new FileNotFoundException($"未找到更新器: {updaterPath}，请确保 Updater 目录随主程序一起发布");

            var mainExe = Application.ExecutablePath;
            var workDir = AppContext.BaseDirectory;

            var psi = new ProcessStartInfo
            {
                FileName = updaterPath,
                Arguments = $"\"{updatePackagePath}\" \"{mainExe}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workDir
            };
            Process.Start(psi);

            // 主程序立即退出，让 updater 能替换被锁定的文件
            Application.Exit();
        }

        /// <summary>语义化版本比较：a > b 返回 1，a == b 返回 0，a < b 返回 -1</summary>
        private static int CompareVersion(string a, string b)
        {
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
    }
}
