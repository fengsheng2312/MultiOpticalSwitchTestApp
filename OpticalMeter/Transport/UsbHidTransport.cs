using OpticalMeter.Contract;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpticalMeter.Transport
{
    public class UsbHidTransport : ITransport
    {
        private bool _isOpen;
        // 实际项目中引入 HidSharp 或类似库
        // private HidDevice _device;
        // private HidStream _stream;
        public byte[]? ResponseTerminator { get; set; }

        public int ExpectedResponseLength { get; set; }
        public bool IsOpen => _isOpen;

        public Task OpenAsync(string connectionString, CancellationToken ct = default)
        {
            // connectionString 格式: "VID:PID" 如 "1313:8078"
            // 实际实现：通过 HidSharp 打开 HID 设备
            // var vid = int.Parse(connectionString.Split(':')[0], NumberStyles.HexNumber);
            // var pid = int.Parse(connectionString.Split(':')[1], NumberStyles.HexNumber);
            // _device = new HidDeviceLocator().GetDevice(vid, pid);
            // _stream = _device.Open();
            _isOpen = true;
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken ct = default)
        {
            // _stream?.Close();
            _isOpen = false;
            return Task.CompletedTask;
        }

        public Task SendAsync(byte[] data, CancellationToken ct = default)
        {
            // _stream.Write(data, 0, data.Length);
            return Task.CompletedTask;
        }

        public Task<byte[]> ReceiveAsync(int bufferSize = 4096, CancellationToken ct = default)
        {
            // var buffer = new byte[bufferSize];
            // var len = _stream.Read(buffer, 0, buffer.Length);
            // return Task.FromResult(buffer.Take(len).ToArray());
            return Task.FromResult(Array.Empty<byte>());
        }

        public async Task<byte[]> SendReceiveAsync(byte[] request, int bufferSize = 4096, CancellationToken ct = default)
        {
            await SendAsync(request, ct);
            await Task.Delay(50, ct);
            return await ReceiveAsync(bufferSize, ct);
        }

        public void Dispose()
        {
            // _stream?.Dispose();
        }
    }
}