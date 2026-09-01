using OpticalMeter.Contract;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace OpticalMeter.Transport
{
    public class TcpTransport : ITransport
    {
        private TcpClient _client;
        private NetworkStream _stream;

        byte[]? ResponseTerminator { get; set; }
        public int ExpectedResponseLength { get; set; }

        public bool IsOpen => _client?.Connected ?? false;

        byte[]? ITransport.ResponseTerminator { get => ResponseTerminator; set => ResponseTerminator = value; }

        public async Task OpenAsync(string connectionString, CancellationToken ct = default)
        {
            // connectionString 格式: "192.168.1.100:5025"
            var parts = connectionString.Split(':');
            var host = parts[0];
            var port = parts.Length > 1 ? int.Parse(parts[1]) : 5025;

            _client = new TcpClient();
            await _client.ConnectAsync(host, port);
            _stream = _client.GetStream();
        }

        public async Task CloseAsync(CancellationToken ct = default)
        {
            _stream?.Dispose();
            _client?.Dispose();
            _stream = null;
            _client = null;
        }

        public async Task SendAsync(byte[] data, CancellationToken ct = default)
        {
            await _stream.WriteAsync(data, 0, data.Length, ct);
        }

        public async Task<byte[]> ReceiveAsync(int bufferSize = 4096, CancellationToken ct = default)
        {
            var buffer = new byte[bufferSize];
            var len = await _stream.ReadAsync(buffer, 0, buffer.Length, ct);
            var result = new byte[len];
            Array.Copy(buffer, result, len);
            return result;
        }

        public async Task<byte[]> SendReceiveAsync(byte[] request, int bufferSize = 4096, CancellationToken ct = default)
        {
            await SendAsync(request, ct);
            await Task.Delay(100, ct); // 等待设备处理
            return await ReceiveAsync(bufferSize, ct);
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _client?.Dispose();
        }
    }
}