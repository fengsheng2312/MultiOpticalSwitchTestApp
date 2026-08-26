using Free_spaceOpticalCirculatorTestApp;
using OpticalMeter.Contract;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Plugins.GenericScpiPowerMeter
{
    /// <summary>
    /// 可配置的通用 SCPI 功率计
    /// 通过 JSON 配置文件驱动，无需修改代码即可适配不同厂商的设备
    /// </summary>
    public class ConfigurablePowerMeter : IPowerMeter
    {
        private  ITransport _transport;
        private  IProtocol _protocol;
        private  PowerMeterConfig _config;
        private bool _disposed;

        public DeviceInfo Info { get; set; }
        public bool IsConnected => _transport.IsOpen;

        public ConfigurablePowerMeter(ITransport transport, IProtocol protocol, PowerMeterConfig config)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            Info = new DeviceInfo(
                vendor: config.Vendor,
                model: config.Model,
                serialNumber: "N/A",
                firmwareVersion: "N/A"
            );
        }

        public async Task ConnectAsync(string connectionString)
        {
            if (!_transport.IsOpen)
            {
                await _transport.OpenAsync(connectionString);
            }

            // 发送 *IDN? 查询设备信息
            try
            {
                var idnBytes = _protocol.Encode(_config.Commands.GetIdn, null);
                Console.WriteLine($"[DEBUG] 发送 IDN 查询命令: {BitConverter.ToString(idnBytes)}");
                //var response = await _transport.SendReceiveAsync(idnBytes);
                //var idnString = _protocol.Decode(response);
                //Console.WriteLine($"{idnString}");
                //// 解析 IDN 响应: "Vendor,Model,SerialNumber,FirmwareVersion"
                //var parts = idnString.Split(',');
                //if (parts.Length >= 4)
                //{
                //    Info = new DeviceInfo(
                //        vendor: parts[0].Trim(),
                //        model: parts[1].Trim(),
                //        serialNumber: parts[2].Trim(),
                //        firmwareVersion: parts[3].Trim()
                //    );
                //}
            }
            catch
            {
                // IDN 查询失败不影响连接，使用配置文件中的默认信息
                Console.WriteLine($"[DEBUG] IDN 查询失败，使用默认信息");
            }
        }

        public async Task DisconnectAsync()
        {
            await _transport.CloseAsync();
        }

        public async Task<double> ReadPowerAsync()
        {   
            String readChannel = Global.readChannel;
            Console.WriteLine($"当前读取通道: {readChannel}");
            String cmd = _config.Commands.ReadPower.Replace("{CH}", readChannel);
            var cmdBytes = _protocol.Encode(cmd, null);
            Console.WriteLine($"发送读取功率命令: {BitConverter.ToString(cmdBytes)}");
            var response = await _transport.SendReceiveAsync(cmdBytes);
            var rawValue = _protocol.Decode(response);
            // 解析数值并应用转换
            if (double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var power))
            {
                return power * _config.PowerScale + _config.PowerOffset;
            }
            throw new InvalidOperationException($"无法解析功率值: {rawValue}");
        }

        public async Task SetWavelengthAsync(double wavelengthNm)
        {
            var deviceValue = (wavelengthNm * _config.WavelengthScale).ToString("F1", CultureInfo.InvariantCulture);

            var parameters = new Dictionary<string, string>
            {
                ["value"] = deviceValue
            };

            var cmdBytes = _protocol.Encode(_config.Commands.SetWavelength, parameters);
            await _transport.SendAsync(cmdBytes);
        }

        public async Task<double> GetWavelengthAsync()
        {
            var cmdBytes = _protocol.Encode(_config.Commands.GetWavelength, null);
            var response = await _transport.SendReceiveAsync(cmdBytes);
            var rawValue = _protocol.Decode(response);

            if (double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var wavelength))
            {
                return wavelength / _config.WavelengthScale;
            }

            throw new InvalidOperationException($"无法解析波长值: {rawValue}");
        }

        public async Task ZeroAsync()
        {
            var cmdBytes = _protocol.Encode(_config.Commands.Zero, null);
            await _transport.SendAsync(cmdBytes);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _transport.Dispose();
            }
        }
    }
}