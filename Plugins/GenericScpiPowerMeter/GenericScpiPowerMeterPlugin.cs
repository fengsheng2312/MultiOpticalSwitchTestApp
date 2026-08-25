using OpticalMeter.Contract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Plugins.GenericScpiPowerMeter
{
    /// <summary>
    /// 通用 SCPI 功率计插件
    /// 通过读取 configs 目录下的 JSON 文件，动态注册支持的设备型号
    /// </summary>
    public class GenericScpiPowerMeterPlugin : IPowerMeterPlugin
    {
        private readonly Dictionary<string, PowerMeterConfig> _configs = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _supportedModels = new();
        public IReadOnlyList<string> SupportedTransports => new[] { "Serial", "USB-HID", "TCP" };
        public string PluginId => "GenericScpiPowerMeter";

        public IReadOnlyList<string> SupportedProtocols => new[] { "SCPI" };

        public IReadOnlyList<string> SupportedModels => _supportedModels;

        public IReadOnlyList<(string, Func<IProtocol>)> GetProtocolOptions()
        {
            return new List<(string, Func<IProtocol>)>  // ★ 显式指定泛型参数
            {
            };
        }
        public GenericScpiPowerMeterPlugin()
        {
            LoadConfigs();
        }

        /// <summary>
        /// 从 configs 目录加载所有 JSON 配置文件
        /// </summary>
        private void LoadConfigs()
        {
            var configsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "configs");

            if (!Directory.Exists(configsDir))
            {
                // 兼容开发环境：从项目目录加载
                configsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "configs");
            }

            if (!Directory.Exists(configsDir))
            {
                Console.WriteLine($"[GenericScpiPowerMeter] 配置目录不存在: {configsDir}");
                return;
            }

            var jsonFiles = Directory.GetFiles(configsDir, "*.json");
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };
            Console.WriteLine($"[GenericScpiPowerMeter] 正在加载配置文件，共 {jsonFiles.Length} 个文件。");

            foreach (var file in jsonFiles)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var config = JsonSerializer.Deserialize<PowerMeterConfig>(json, options);

                    if (config != null && !string.IsNullOrEmpty(config.Model))
                    {
                        _configs[config.Model] = config;
                        _supportedModels.Add(config.Model);
                        Console.WriteLine($"[GenericScpiPowerMeter] 已加载配置: {config.Vendor} {config.Model}");
                        Console.WriteLine($"读串口指令{config.Commands.ReadPower}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GenericScpiPowerMeter] 加载配置失败: {Path.GetFileName(file)} - {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 创建设备实例
        /// </summary>
        public IPowerMeter Create(ITransport transport, IProtocol protocol, Dictionary<string, string> config)
        {
            var model = config.GetValueOrDefault("model", "");

            if (!_configs.TryGetValue(model, out var deviceConfig))
            {
                // 如果找不到精确匹配的配置，使用默认 SCPI 配置
                deviceConfig = new PowerMeterConfig
                {
                    Vendor = "Unknown",
                    Model = model,
                    Protocol = "SCPI"
                };
            }
            Console.WriteLine($"[GenericScpiPowerMeter] 创建设备实例: {deviceConfig.Vendor} {deviceConfig.Model}");
            return new ConfigurablePowerMeter(transport, protocol, deviceConfig);
        }
    }
}