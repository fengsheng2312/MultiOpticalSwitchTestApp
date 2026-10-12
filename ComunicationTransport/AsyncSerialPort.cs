using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MultiOpticalSwitchTestApp.ComunicationTransport
{
    /// <summary>
    /// 异步串口封装类，兼容SCPI文本、GBK中文应答、二进制数据
    /// </summary>
    public class AsyncSerialPort : IDisposable
    {
        #region 基础字段
        private SerialPort _serialPort;
        private readonly object _rwLock = new();
        private bool _disposed;
        private CancellationTokenSource _receiveCts;
        private readonly StringBuilder _receiveBuffer = new();
        #endregion

        #region 配置参数
        public string PortName { get; }
        public int BaudRate { get; }
        public int DataBits { get; }
        public Parity Parity { get; }
        public StopBits StopBits { get; }
        public int ReadTimeoutMs { get; set; } = 3000;
        public int WriteTimeoutMs { get; set; } = 3000;

        /// <summary>SCPI报文分隔符，仅用于OnDataReceived事件</summary>
        public string ReceiveSeparator { get; set; } = "\r\n";

        public bool IsOpen => _serialPort != null && _serialPort.IsOpen;
        #endregion

        #region 事件
        /// <summary>【SCPI专用】收到以ReceiveSeparator结尾的完整文本报文，内部使用ASCII解码</summary>
        public event Action<string> OnDataReceived;

        /// <summary>【通用】原始接收字节，不做编码、不做分割，二进制/GBK/ASCII全部靠这个</summary>
        public event Action<byte[]> OnRawDataReceived;

        /// <summary>串口意外断开</summary>
        public event Action OnDisconnected;

        /// <summary>通信异常</summary>
        public event Action<Exception> OnError;
        #endregion

        public AsyncSerialPort(string portName, int baudRate, int dataBits = 8, Parity parity = Parity.None, StopBits stopBits = StopBits.One)
        {
            PortName = portName;
            BaudRate = baudRate;
            DataBits = dataBits;
            Parity = parity;
            StopBits = stopBits;
        }

        #region 打开关闭
        public void Open()
        {
            if (IsOpen) return;

            lock (_rwLock)
            {
                _serialPort = new SerialPort(PortName, BaudRate, Parity, DataBits, StopBits)
                {
                    ReadTimeout = ReadTimeoutMs,
                    WriteTimeout = WriteTimeoutMs
                };
                _serialPort.Open();

                _receiveCts = new CancellationTokenSource();
                _ = ReceiveLoopAsync(_receiveCts.Token);
            }
        }

        public void Close()
        {
            lock (_rwLock)
            {
                if (!IsOpen) return;

                _receiveCts?.Cancel();
                try
                {
                    if (_serialPort.IsOpen)
                        _serialPort.Close();
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(ex);
                }
                _serialPort?.Dispose();
                _serialPort = null;
                _receiveBuffer.Clear();
            }
        }
        #endregion

        #region 异步发送
        /// <summary>SCPI字符串发送，自动追加分隔符，ASCII编码</summary>
        public async Task WriteAsync(string data, CancellationToken token = default)
        {
            if (!IsOpen)
                throw new InvalidOperationException("串口未打开");

            string sendStr = data + ReceiveSeparator;
            byte[] buffer = Encoding.ASCII.GetBytes(sendStr);

            lock (_rwLock)
            {
                if (!IsOpen) throw new InvalidOperationException("串口已断开");
            }
            await _serialPort.BaseStream.WriteAsync(buffer, 0, buffer.Length, token);
        }

        /// <summary>原始字节发送（二进制指令）</summary>
        public async Task WriteBytesAsync(byte[] buffer)
        {
            if (!IsOpen)
                throw new InvalidOperationException("串口未打开");
            await _serialPort.BaseStream.WriteAsync(buffer, 0, buffer.Length);
            await _serialPort.BaseStream.FlushAsync();
        }

        /// <summary>SCPI查询问答</summary>
        public async Task<string> QueryAsync(string cmd, int timeoutMs = 2000)
        {
            var tcs = new TaskCompletionSource<string>();
            void Handler(string recv)
            {
                tcs.TrySetResult(recv.Trim());
            }

            try
            {
                OnDataReceived += Handler;
                await WriteAsync(cmd);

                using var cts = new CancellationTokenSource(timeoutMs);
                cts.Token.Register(() => tcs.TrySetException(new TimeoutException("查询响应超时")));

                return await tcs.Task;
            }
            finally
            {
                OnDataReceived -= Handler;
            }
        }
        #endregion

        #region 持续接收循环
        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            byte[] tempBuf = new byte[1024];
            try
            {
                while (!token.IsCancellationRequested && IsOpen)
                {
                    int readLen = await _serialPort.BaseStream.ReadAsync(tempBuf, 0, tempBuf.Length, token);
                    if (readLen <= 0) continue;

                    // 抛出原始字节事件（最重要，兼容GBK、二进制）
                    byte[] rawData = new byte[readLen];
                    Array.Copy(tempBuf, rawData, readLen);
                    OnRawDataReceived?.Invoke(rawData);

                    // 原有SCPI逻辑：ASCII解码，按\r\n分割报文，供OnDataReceived
                    string recvStr = Encoding.ASCII.GetString(tempBuf, 0, readLen);
                    _receiveBuffer.Append(recvStr);

                    while (true)
                    {
                        string allData = _receiveBuffer.ToString();
                        int idx = allData.IndexOf(ReceiveSeparator);
                        if (idx < 0) break;

                        string packet = allData.Substring(0, idx);
                        _receiveBuffer.Remove(0, idx + ReceiveSeparator.Length);
                        OnDataReceived?.Invoke(packet);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                //正常关闭
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                OnDisconnected?.Invoke();
            }
        }
        #endregion

        #region Dispose
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                Close();
            }
            _disposed = true;
        }
        #endregion
    }
}
