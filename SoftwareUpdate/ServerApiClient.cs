using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace MultiOpticalSwitchTestApp.SoftwareUpdate
{
    public class ServerApiClient : IDisposable
    {
        private readonly HttpClient _http;
        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

        public string BaseAddress { get; }
        public string CacheDirectory { get; }

        public ServerApiClient(string baseAddress, string apiKey)
        {
            BaseAddress = baseAddress.TrimEnd('/');
            _http = new HttpClient
            {
                BaseAddress = new Uri(BaseAddress),
                Timeout = TimeSpan.FromSeconds(30)
            };
            _http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

            // 本地缓存目录：%LOCALAPPDATA%\HexClient\HexCache
            CacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HexClient", "HexCache");
            Directory.CreateDirectory(CacheDirectory);
        }

        /// <summary>健康检查</summary>
        public async Task<bool> HealthAsync()
        {
            try
            {
                var resp = await _http.GetAsync("/api/health");
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show($"服务器连接失败！\n\nHTTP 状态码: {(int)resp.StatusCode} {resp.StatusCode}\n响应内容: {body}\n\n服务器地址: {BaseAddress}", "连接失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                var msg = $"服务器连接失败！\n\n异常类型: {ex.GetType().Name}\n异常消息: {ex.Message}\n\n服务器地址: {BaseAddress}";
                if (ex.InnerException is not null)
                    msg += $"\n\n内部异常: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
                msg += $"\n\n堆栈: {ex.StackTrace}";
                MessageBox.Show(msg, "连接失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
        /// <summary>下载固件（带进度），优先读本地缓存；返回字节数组</summary>
        /// <param name="fileName">固件文件名</param>
        /// <param name="expectedSha256">期望的 SHA256（用于缓存命中判断和校验），为 null 则不缓存</param>
        public async Task<byte[]> DownloadHexAsync(string fileName, string? expectedSha256 = null, IProgress<double>? progress = null)
        {
            // 1. 缓存命中：本地已有相同 SHA256 的文件，直接读本地（离线兜底）
            if (!string.IsNullOrEmpty(expectedSha256))
            {
                var cachedPath = GetCachedPath(expectedSha256, fileName);
                if (File.Exists(cachedPath))
                {
                    progress?.Report(1.0);
                    return await File.ReadAllBytesAsync(cachedPath);
                }
            }

            // 2. 从服务器下载
            var resp = await _http.GetAsync($"/api/hex/download?name={Uri.EscapeDataString(fileName)}",
                                             HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();

            var total = resp.Content.Headers.ContentLength ?? 0;
            await using var stream = await resp.Content.ReadAsStreamAsync();
            using var ms = new MemoryStream();
            var buffer = new byte[65536];
            long readTotal = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                await ms.WriteAsync(buffer.AsMemory(0, read));
                readTotal += read;
                if (total > 0) progress?.Report((double)readTotal / total);
            }
            var data = ms.ToArray();

            // 3. 写入缓存（按 SHA256 命名，避免重复下载）
            if (!string.IsNullOrEmpty(expectedSha256))
            {
                var cachePath = GetCachedPath(expectedSha256, fileName);
                try { await File.WriteAllBytesAsync(cachePath, data); } catch { /* 缓存写入失败不影响主流程 */ }
            }

            return data;
        }

        /// <summary>缓存文件路径：{CacheDirectory}\{SHA256前8位}_{纯文件名}</summary>
        private string GetCachedPath(string sha256, string fileName)
            => Path.Combine(CacheDirectory, $"{sha256.Substring(0, Math.Min(8, sha256.Length))}_{Path.GetFileName(fileName)}");

        /// <summary>清空本地缓存</summary>
        public void ClearCache()
        {
            if (Directory.Exists(CacheDirectory))
            {
                foreach (var f in Directory.GetFiles(CacheDirectory))
                    try { File.Delete(f); } catch { }
            }
        }

        /// <summary>SHA256 校验下载的固件</summary>
        public static string ComputeSha256(byte[] data)
        {
            var hash = SHA256.HashData(data);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>上传本地固件文件到服务器（multipart/form-data）</summary>
        public async Task<bool> UploadHexAsync(string filePath)
        {
            await using var fs = File.OpenRead(filePath);
            using var content = new StreamContent(fs);
            using var form = new MultipartFormDataContent();
            form.Add(content, "file", Path.GetFileName(filePath));
            var resp = await _http.PostAsync("/api/hex/upload", form);
            return resp.IsSuccessStatusCode;
        }

        /// <summary>删除服务器上的固件文件</summary>
        public async Task<bool> DeleteHexAsync(string fileName)
        {
            var resp = await _http.DeleteAsync($"/api/hex/delete?name={Uri.EscapeDataString(fileName)}");
            return resp.IsSuccessStatusCode;
        }

        /// <summary>触发服务器扫描 HexRepository 目录重新生成 manifest，返回固件数量</summary>
        public async Task<int> RefreshHexAsync()
        {
            var resp = await _http.PostAsync("/api/hex/refresh", null);
            if (!resp.IsSuccessStatusCode) return -1;
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("count", out var c) ? c.GetInt32() : -1;
        }

        public void Dispose() => _http.Dispose();
    }
}
