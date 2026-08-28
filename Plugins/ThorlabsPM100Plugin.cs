using OpticalMeter.Contract;
using System;
using System.Collections.Generic;

namespace Plugins.ThorlabsPM100
{
    public class ThorlabsPM100Plugin : IPowerMeterPlugin
    {
        public string PluginId => "thorlabs-pm100";

        public IReadOnlyList<string> SupportedProtocols => new[] { "VendorPrivate" };

        public IReadOnlyList<string> SupportedModels => new[] { "PM100D", "PM100USB" };
        public IReadOnlyList<string> SupportedTransports => new[] { "USB-HID", "Serial" };

        public IReadOnlyList<string> GetSupportedTransports(string model)
        {
            return SupportedTransports;
        }

        public IPowerMeter Create(
            ITransport transport,
            IProtocol protocol,
            Dictionary<string, string> config)
        {
            var meter = new ThorlabsPM100Driver(transport, protocol);
            meter.Info = new DeviceInfo(
                vendor: "Thorlabs",
                model: config.GetValueOrDefault("model", "PM100D"),
                serialNumber: config.GetValueOrDefault("serial", ""),
                firmwareVersion: ""
            );
            return meter;
        }
        public IReadOnlyList<(string, Func<IProtocol>)> GetProtocolOptions()
        {
            return new List<(string, Func<IProtocol>)>  // ★ 显式指定泛型参数
            {
            };
        }
    }
}