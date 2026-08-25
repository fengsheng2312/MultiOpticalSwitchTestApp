using OpticalMeter.Contract;
using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace OpticalMeter.Transport
{
    public class SerialTransport : ITransport
    {
        private SerialPort _port;

        public bool IsOpen => _port?.IsOpen ?? false;

        public Task OpenAsync(string connectionString, CancellationToken ct = default)
        {
            // connectionString 格式: "COM3:9600:8:N:1"
            var parts = connectionString.Split(':');
            _port = new SerialPort
            {
                PortName = parts[0],
                BaudRate = parts.Length > 1 ? int.Parse(parts[1]) : 9600,
                DataBits = parts.Length > 2 ? int.Parse(parts[2]) : 8,
                Parity = parts.Length > 3 ? ParseParity(parts[3]) : Parity.None,
                StopBits = parts.Length > 4 ? ParseStopBits(parts[4]) : StopBits.One,
                ReadTimeout = 3000,
                WriteTimeout = 3000
            };
            _port.Open();
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken ct = default)
        {
            _port?.Close();
            _port?.Dispose();
            _port = null;
            return Task.CompletedTask;
        }

        public async Task SendAsync(byte[] data, CancellationToken ct = default)
        {
            await _port.BaseStream.WriteAsync(data, 0, data.Length, ct);
        }

        public async Task<byte[]> ReceiveAsync(int bufferSize = 4096, CancellationToken ct = default)
        {
            var buffer = new byte[bufferSize];
            var len = await _port.BaseStream.ReadAsync(buffer, 0, buffer.Length, ct);
            var result = new byte[len];
            Array.Copy(buffer, result, len);
            return result;
        }

        public async Task<byte[]> SendReceiveAsync(byte[] request, int bufferSize = 4096, CancellationToken ct = default)
        {
            await SendAsync(request, ct);
            await Task.Delay(100, ct);
            return await ReceiveAsync(bufferSize, ct);
        }

        public void Dispose()
        {
            _port?.Dispose();
        }

        private Parity ParseParity(string s) => s.ToUpper() switch
        {
            "N" => Parity.None,
            "O" => Parity.Odd,
            "E" => Parity.Even,
            _ => Parity.None
        };

        private StopBits ParseStopBits(string s) => s switch
        {
            "1" => StopBits.One,
            "2" => StopBits.Two,
            _ => StopBits.One
        };
    }
}