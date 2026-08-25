using System;
using System.Collections.Generic;

namespace OpticalMeter.Contract
{
    public interface IPowerMeterPlugin
    {
        string PluginId { get; }
        IReadOnlyList<string> SupportedModels { get; }
        IReadOnlyList<string> SupportedProtocols { get; }

        IReadOnlyList<string> SupportedTransports => new[] { "Serial" };

        // ★ 新增：插件提供协议选项及对应的工厂方法
        IReadOnlyList<(string DisplayName, Func<IProtocol> Factory)> GetProtocolOptions();

        IPowerMeter Create(ITransport transport, IProtocol protocol, Dictionary<string, string> config);
    }
}