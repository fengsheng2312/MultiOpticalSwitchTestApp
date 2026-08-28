using OpticalMeter.Contract;
using OpticalMeter.Protocol;   // ✅ 补上，ScpiProtocol 在这个命名空间
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;              // ✅ 补上，Select 扩展方法需要
using System.Text.Json;
using System.Text.Json.Serialization;


namespace Plugins.GenericScpiPowerMeter
{
    public enum TransportType
    {
        Serial,
        Tcp,
        UsbTmc,
        Simulate
    }
    /// <summary>
    /// 通用 SCPI 功率计插件
    /// 通过读取 configs 目录下的 JSON 文件，动态注册支持的设备型号
    /// </summary>
    public class GenericScpiPowerMeterPlugin : IPowerMeterPlugin
    {
        public readonly Dictionary<string, PowerMeterConfig> _configs = new(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyDictionary<string, PowerMeterConfig> DeviceConfigs => _configs;
        private readonly List<string> _supportedModels = new();

        // SCPI 插件：从 DeviceConfigs 按型号取
        public IReadOnlyList<string> GetSupportedTransports(string model)
        {
            if (DeviceConfigs.TryGetValue(model, out var cfg) && cfg.SupportedTransports != null)
                return cfg.SupportedTransports.Select(t => t.ToString()).ToList();
            return Array.Empty<string>();
        }
        private readonly List<string> _supportedTransport = new();
        public IReadOnlyList<string> SupportedTransports => _supportedTransport;
        public string PluginId => "GenericScpiPowerMeter";

        public IReadOnlyList<string> SupportedProtocols => new[] { "SCPI" };

        public IReadOnlyList<string> SupportedModels => _supportedModels;

        // ✅ 顺便补上 GetProtocolOptions（之前缺失导致协议走回退分支）
        public IReadOnlyList<(string, Func<IProtocol>)> GetProtocolOptions()
        {
            return new List<(string, Func<IProtocol>)>
            {
                ("SCPI", new Func<IProtocol>(() => new ScpiProtocol())),
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
                ReadCommentHandling = JsonCommentHandling.Skip,
                // 关键：字符串和枚举互相转换
                Converters =
                {
                    new JsonStringEnumConverter()
                }
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
                        //兜底：兼容json SupportedTransports:null
                        config.SupportedTransports ??= new List<TransportType>();

                        //数组为空时，把DefaultTransport自动补进去，防止UI下拉为空
                        if (config.SupportedTransports.Count == 0)
                        {
                            if (Enum.TryParse<TransportType>(config.DefaultTransport, true, out var defT))
                            {
                                config.SupportedTransports.Add(defT);
                                Console.WriteLine($"[ConfigCompat] {config.Model} SupportedTransports为空，自动补DefaultTransport={defT}");
                            }
                            else
                            {
                                Console.WriteLine($"[ERROR] {config.Model} DefaultTransport[{config.DefaultTransport}]无法解析为枚举！");
                            }
                        }

                        _configs[config.Model] = config;
                        _supportedModels.Add(config.Model);
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