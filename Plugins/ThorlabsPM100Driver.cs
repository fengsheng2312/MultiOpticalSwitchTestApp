using OpticalMeter.Contract;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Plugins.ThorlabsPM100
{
    /// <summary>
    /// Thorlabs PM100 驱动 —— 使用私有二进制协议
    /// 演示：同一个 IPowerMeter 接口，完全不同的协议实现
    /// </summary>
    public class ThorlabsPM100Driver : IPowerMeter
    {
        private readonly ITransport _transport;
        private readonly IProtocol _protocol;
        private bool _isConnected;

        public ThorlabsPM100Driver(ITransport transport, IProtocol protocol)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
        }

        public DeviceInfo Info { get; set; }

        public bool IsConnected => _isConnected;

        public async Task ConnectAsync(string connectionString)
        {
            await _transport.OpenAsync(connectionString);
            _isConnected = true;
        }

        public async Task DisconnectAsync()
        {
            await _transport.CloseAsync();
            _isConnected = false;
        }

        public async Task<double> ReadPowerAsync()
        {
            var requestBytes = _protocol.Encode("readPower", new Dictionary<string, string>());
            var responseBytes = await _transport.SendReceiveAsync(requestBytes);
            var result = _protocol.Decode(responseBytes);
            return double.Parse(result, CultureInfo.InvariantCulture);
        }

        public async Task SetWavelengthAsync(double wavelengthNm)
        {
            var parameters = new Dictionary<string, string>
            {
                { "value", wavelengthNm.ToString(CultureInfo.InvariantCulture) }
            };
            var requestBytes = _protocol.Encode("setWavelength", parameters);
            await _transport.SendAsync(requestBytes);
        }

        public async Task<double> GetWavelengthAsync()
        {
            var requestBytes = _protocol.Encode("getWavelength", new Dictionary<string, string>());
            var responseBytes = await _transport.SendReceiveAsync(requestBytes);
            var result = _protocol.Decode(responseBytes);
            return double.Parse(result, CultureInfo.InvariantCulture);
        }

        public async Task ZeroAsync()
        {
            var requestBytes = _protocol.Encode("zero", new Dictionary<string, string>());
            await _transport.SendAsync(requestBytes);
        }

        public void Dispose()
        {
            _transport?.Dispose();
        }
    }
}