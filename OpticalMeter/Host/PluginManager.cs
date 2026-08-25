using OpticalMeter.Contract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Globalization;


namespace OpticalMeter.Host
{
    /// <summary>
    /// 插件管理器 —— 负责扫描、加载和管理所有插件
    /// </summary>
    public class PluginManager
    {
        private readonly List<IPowerMeterPlugin> _plugins = new();
        private readonly List<IProtocol> _protocols = new();
        private readonly List<IPowerMeter> _powerMeters = new();
        /// <summary>已加载的插件列表</summary>
        public IReadOnlyList<IPowerMeterPlugin> Plugins => _plugins;
        public IReadOnlyList<IProtocol> Protocols => _protocols;
        public IReadOnlyList<IPowerMeter> PowerMeters => _powerMeters;
        public void RegisterPlugin(IPowerMeterPlugin plugin)
        {
            // 避免重复注册
            if (_plugins.Any(p => p.PluginId == plugin.PluginId))
                return;
            _plugins.Add(plugin);
        }
        /// <summary>
        /// 扫描指定目录下的所有 DLL，查找并加载实现了 IPowerMeterPlugin 的类型
        /// </summary>
        public void ScanAndLoad(string pluginsDirectory)
        {
            if (!Directory.Exists(pluginsDirectory))
            {
                Console.WriteLine($"[PluginManager] 插件目录不存在: {pluginsDirectory}");
                return;
            }

            var dllFiles = Directory.GetFiles(pluginsDirectory, "*.dll", SearchOption.TopDirectoryOnly);
            Console.WriteLine($"[PluginManager] 扫描到 {dllFiles.Length} 个 DLL 文件");

            foreach (var dllPath in dllFiles)
            {
                try
                {
                    LoadPlugin(dllPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PluginManager] 加载失败: {Path.GetFileName(dllPath)} - {ex.Message}");
                }
            }

            Console.WriteLine($"[PluginManager] 共加载 {_plugins.Count} 个插件");
        }

        private void LoadPlugin(string dllPath)
        {
            var assembly = Assembly.LoadFrom(dllPath);
            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(IPowerMeterPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();
            var protocolTypes = assembly.GetTypes()
                .Where(t => typeof(IProtocol).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();
            var powerMeterTypes = assembly.GetTypes()
    .Where(t => typeof(IPowerMeter).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
    .ToList();
            foreach (var type in pluginTypes)
            {
                if (Activator.CreateInstance(type) is IPowerMeterPlugin plugin)
                {
                    _plugins.Add(plugin);
                    Console.WriteLine($"[PluginManager] 已加载插件: {plugin.PluginId} " +
                        $"(支持协议: {string.Join(", ", plugin.SupportedProtocols)}, " +
                        $"支持型号: {string.Join(", ", plugin.SupportedModels)})");
                }
                //if (Activator.CreateInstance(type) is IPowerMeter powerMeter)
                //{
                //    _powerMeters.Add(powerMeter);
                //}
            }
            foreach (var type in protocolTypes)
            {
                if (Activator.CreateInstance(type) is IProtocol protocol)
                {
                    _protocols.Add(protocol);
                    Console.WriteLine($"[PluginManager] 已加载协议: {protocol.Name}");
                }
            }
        }

        /// <summary>
        /// 根据设备型号查找匹配的插件
        /// </summary>
        public IPowerMeterPlugin? FindPlugin(string model, string? protocolName = null,String ? transportName = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(protocolName))
                {
                    // 优先精确匹配：型号 + 协议都匹配
                    var exact = _plugins.FirstOrDefault(p =>
                        p.SupportedModels.Any(m => m.Equals(model, StringComparison.OrdinalIgnoreCase)) &&
                        p.SupportedProtocols.Any(pr => pr.Equals(protocolName, StringComparison.OrdinalIgnoreCase)) &&
                        p.SupportedTransports.Any(t => t.Equals(transportName, StringComparison.OrdinalIgnoreCase))
                        );
                    if (exact != null)
                    {
                        Console.WriteLine($"[PluginManager] 精确匹配插件:类型：{exact.GetType().Name} {exact.PluginId} (型号: {model}, 协议: {protocolName}, 传输: {transportName})");
                        return exact;
                    }
                }
                return _plugins.FirstOrDefault(p =>
                p.SupportedModels.Any(m => m.Equals(model, StringComparison.OrdinalIgnoreCase)));

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PluginManager] 查找插件异常: {ex.Message}");
                return null;
            }
            Console.WriteLine($"[PluginManager] 未找到精确匹配插件 (型号: {model}, 协议: {protocolName}, 传输: {transportName})，尝试回退匹配。");
            // 回退：只匹配型号
        }

        /// <summary>
        /// 根据协议名称查找匹配的插件
        /// </summary>
        public IPowerMeterPlugin? FindPluginByProtocol(string protocol)
        {
            return _plugins.FirstOrDefault(p =>
                p.SupportedProtocols.Any(s => s.Equals(protocol, StringComparison.OrdinalIgnoreCase)));
        }
    }

    //internal static class Global
    //{
    //    // 公共全局协议映射
    //    public static readonly Dictionary<string, IProtocol> protocols = new Dictionary<string, IProtocol>();
    //    internal static IPowerMeter opticalPowermeter;
    //}
}