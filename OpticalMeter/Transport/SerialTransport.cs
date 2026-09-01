using OpticalMeter.Contract;
using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace OpticalMeter.Transport
{
    public class SerialTransport : ITransport
    {
        private readonly object _stateLock = new();
        private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

        public int ExpectedResponseLength { get; set; }

        private readonly SemaphoreSlim _ioLock = new(1, 1);
        private SerialPort? _port;
        private CancellationTokenSource? _connectionCts;
        private bool _disposed;

        private const int IoDrainTimeoutMs = 1000;
        private const int PortCloseTimeoutMs = 2000;
        private const int OpenRetryCount = 3;
        private const int OpenRetryDelayMs = 500;

        /// <summary>
        /// BaseStream.ReadAsync does not reliably honor SerialPort.ReadTimeout on Windows.
        /// Keep an explicit timeout so a missing response cannot leave disconnect waiting
        /// forever for a read that is still in progress.
        /// </summary>
        public int ReadTimeoutMs { get; set; } = 300;

        public int WriteTimeoutMs { get; set; } = 3000;

        public bool IsOpen
        {
            get
            {
                lock (_stateLock)
                {
                    return _port?.IsOpen ?? false;
                }
            }
        }

        public async Task OpenAsync(string connectionString, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Serial connection string cannot be empty.", nameof(connectionString));

            await _lifecycleLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                lock (_stateLock)
                {
                    if (_disposed)
                        throw new ObjectDisposedException(nameof(SerialTransport));
                    if (_port?.IsOpen == true)
                        return;
                }

                // connectionString 格式: "COM3:9600:8:N:1"
                var parts = connectionString.Split(':');
                var portName = parts[0].Trim();
                var baudRate = parts.Length > 1 ? int.Parse(parts[1]) : 9600;
                var dataBits = parts.Length > 2 ? int.Parse(parts[2]) : 8;
                var parity = parts.Length > 3 ? ParseParity(parts[3]) : Parity.None;
                var stopBits = parts.Length > 4 ? ParseStopBits(parts[4]) : StopBits.One;

                SerialPort? port = null;

                for (int attempt = 0; attempt < OpenRetryCount; attempt++)
                {
                    ct.ThrowIfCancellationRequested();
                    port = new SerialPort
                    {
                        PortName = portName,
                        BaudRate = baudRate,
                        DataBits = dataBits,
                        Parity = parity,
                        StopBits = stopBits,
                        ReadTimeout = ReadTimeoutMs,
                        WriteTimeout = WriteTimeoutMs
                    };

                    try
                    {
                        port.Open();
                        break;
                    }
                    catch (UnauthorizedAccessException) when (attempt < OpenRetryCount - 1)
                    {
                        // 端口被占用，可能是上一次断开后原生句柄尚未释放
                        Console.WriteLine($"[WARN] {portName} busy, retry {attempt + 1}/{OpenRetryCount} after {OpenRetryDelayMs}ms...");
                        port.Dispose();
                        port = null;
                        await Task.Delay(OpenRetryDelayMs, ct).ConfigureAwait(false);
                    }
                    catch
                    {
                        port.Dispose();
                        throw;
                    }
                }

                if (port == null || !port.IsOpen)
                    throw new UnauthorizedAccessException($"Failed to open {portName} after {OpenRetryCount} attempts.");

                lock (_stateLock)
                {
                    if (_disposed)
                    {
                        port.Close();
                        port.Dispose();
                        throw new ObjectDisposedException(nameof(SerialTransport));
                    }
                    _port = port;
                    _connectionCts = new CancellationTokenSource();
                }
            }
            finally
            {
                _lifecycleLock.Release();
            }
        }

        public async Task CloseAsync(CancellationToken ct = default)
        {
            await _lifecycleLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                SerialPort? port;
                CancellationTokenSource? connectionCts;
                lock (_stateLock)
                {
                    port = _port;
                    connectionCts = _connectionCts;
                    _port = null;
                    _connectionCts = null;
                }

                if (port == null)
                {
                    connectionCts?.Dispose();
                    return;
                }

                // Cancel any BaseStream read/write before closing the native handle.
                // Wait for that operation to unwind before touching the native handle.
                connectionCts?.Cancel();

                var ioDrainTask = _ioLock.WaitAsync(CancellationToken.None);
                if (await Task.WhenAny(
                        ioDrainTask,
                        Task.Delay(IoDrainTimeoutMs)).ConfigureAwait(false) == ioDrainTask)
                {
                    await ioDrainTask.ConfigureAwait(false);
                    _ioLock.Release();
                }
                else
                {
                    Console.WriteLine($"[WARN] Serial I/O did not cancel within {IoDrainTimeoutMs} ms; forcing port close.");
                    var ctsToDispose = connectionCts;
                    connectionCts = null;
                    _ = ioDrainTask.ContinueWith(
                        completedTask =>
                        {
                            if (completedTask.Status == TaskStatus.RanToCompletion)
                                _ioLock.Release();
                            ctsToDispose?.Dispose();
                        },
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                }

                Console.WriteLine($"Closing serial port {port.PortName}...");
                try
                {
                    // SerialPort.Close is synchronous, so keep it off the WinForms UI thread.
                    var closeTask = Task.Run(() =>
                    {
                        try
                        {
                            if (port.IsOpen)
                            {
                                try { port.DiscardOutBuffer(); } catch { }
                                try { port.DiscardInBuffer(); } catch { }

                                // ★ 核心修复：先关 BaseStream，内部对 FileStream 句柄
                                //   执行 CancelIoEx，撤销挂起的 ReadFile/WriteFile IRP，
                                //   确保串口句柄可被操作系统释放。
                                try { port.BaseStream.Close(); } catch { }

                                port.Close();
                            }
                        }
                        catch (IOException ex)
                        {
                            // 串口已拔出 / 驱动异常，忽略
                            Console.WriteLine($"[WARN] Close IO exception: {ex.Message}");
                        }
                        finally
                        {
                            port.Dispose();
                        }
                    });

                    if (await Task.WhenAny(
                            closeTask,
                            Task.Delay(PortCloseTimeoutMs)).ConfigureAwait(false) == closeTask)
                    {
                        await closeTask.ConfigureAwait(false);
                        Console.WriteLine($"Serial port {port.PortName} closed and disposed.");
                    }
                    else
                    {
                        Console.WriteLine($"[WARN] Serial port {port.PortName} close exceeded {PortCloseTimeoutMs} ms; cleanup continues in background.");
                        _ = closeTask.ContinueWith(
                            completedTask =>
                            {
                                if (completedTask.IsFaulted)
                                    Console.WriteLine($"[WARN] Background serial close failed: {completedTask.Exception?.GetBaseException().Message}");
                            },
                            CancellationToken.None,
                            TaskContinuationOptions.ExecuteSynchronously,
                            TaskScheduler.Default);
                    }
                }
                finally
                {
                    connectionCts?.Dispose();
                }
            }
            finally
            {
                _lifecycleLock.Release();
            }
        }

        public async Task SendAsync(byte[] data, CancellationToken ct = default)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Data cannot be empty.", nameof(data));

            await _ioLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var operation = BeginOperation(ct);
                using (operation.TokenSource)
                using (var writeCts = CancellationTokenSource.CreateLinkedTokenSource(operation.TokenSource.Token))
                {
                    if (WriteTimeoutMs > 0)
                        writeCts.CancelAfter(WriteTimeoutMs);

                    try
                    {
                        await operation.Port.BaseStream.WriteAsync(
                            data, 0, data.Length, writeCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested && !operation.TokenSource.IsCancellationRequested)
                    {
                        throw new TimeoutException($"Serial write timed out after {WriteTimeoutMs} ms.");
                    }
                }
            }
            finally
            {
                _ioLock.Release();
            }
        }

        public async Task<byte[]> ReceiveAsync(int bufferSize = 4096, CancellationToken ct = default)
        {
            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));

            await _ioLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var operation = BeginOperation(ct);
                using (operation.TokenSource)
                using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(operation.TokenSource.Token))
                {
                    timeoutCts.CancelAfter(ReadTimeoutMs);
                    var buffer = new byte[bufferSize];
                    try
                    {
                        var len = await operation.Port.BaseStream.ReadAsync(
                            buffer, 0, buffer.Length, timeoutCts.Token).ConfigureAwait(false);
                        return buffer[..len];
                    }
                    catch (OperationCanceledException) when (
                        !ct.IsCancellationRequested && !operation.TokenSource.IsCancellationRequested)
                    {
                        // 超时后清理输入缓冲区，防止原生残留数据干扰下次读取
                        try { operation.Port.DiscardInBuffer(); } catch { }
                        throw new TimeoutException($"Serial response timed out after {ReadTimeoutMs} ms.");
                    }
                }
            }
            finally
            {
                _ioLock.Release();
            }
        }
        /// <summary>
        /// 发送请求并读取响应，直到遇到指定结束符或整体超时。
        /// 适用于设备响应可能分包到达的场景（如 SCPI 协议以 \n 结尾）。
        /// </summary>
        /// <param name="request">发送的数据</param>
        /// <param name="terminator">响应结束符，如 new byte[] { 0x0A } (\n)</param>
        /// <param name="maxResponseSize">响应最大字节数，防止无限累积</param>
        /// <param name="overallTimeoutMs">从开始读到读完的总超时（毫秒）</param>


        /// <summary>
        /// 发送请求并读取固定长度的响应。
        /// 适用于协议头中包含长度字段、或响应长度固定的场景。
        /// </summary>

        /// <summary>
        /// 响应结束符，默认 \n（SCPI 协议）。收到此字节序列后认为响应完整。
        /// 设为 null 或空数组则退化为单次读取（原行为）。
        /// </summary>
        public byte[]? ResponseTerminator { get; set; } = new byte[] { 0x0A };
        // \r\n => { 0x0D, 0x0A };
        /// <summary>
        /// 读取响应的整体超时（毫秒），从开始读到读完的总时间上限。
        /// </summary>
        public int OverallReadTimeoutMs { get; set; } = 1000;
        public async Task<byte[]> SendReceiveAsync(byte[] request, int bufferSize = 4096, CancellationToken ct = default)
        {
            if (request == null || request.Length == 0)
                throw new ArgumentException("Request cannot be empty.", nameof(request));
            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));

            await _ioLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var operation = BeginOperation(ct);
                using (operation.TokenSource)
                {
                    // 发送请求（带写超时）
                    using (var writeCts = CancellationTokenSource.CreateLinkedTokenSource(operation.TokenSource.Token))
                    {
                        if (WriteTimeoutMs > 0)
                            writeCts.CancelAfter(WriteTimeoutMs);
                        try
                        {
                            await operation.Port.BaseStream.WriteAsync(
                                request, 0, request.Length, writeCts.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (!ct.IsCancellationRequested && !operation.TokenSource.IsCancellationRequested)
                        {
                            throw new TimeoutException($"Serial write timed out after {WriteTimeoutMs} ms.");
                        }
                    }

                    await Task.Delay(100, operation.TokenSource.Token).ConfigureAwait(false);

                    // 未配置结束符 → 单次读取（兼容原行为）
                    if (ResponseTerminator == null || ResponseTerminator.Length == 0)
                    {
                        using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(operation.TokenSource.Token))
                        {
                            timeoutCts.CancelAfter(ReadTimeoutMs);
                            var buffer = new byte[bufferSize];
                            try
                            {
                                var len = await operation.Port.BaseStream.ReadAsync(
                                    buffer, 0, buffer.Length, timeoutCts.Token).ConfigureAwait(false);
                                return buffer[..len];
                            }
                            catch (OperationCanceledException) when (
                                !ct.IsCancellationRequested && !operation.TokenSource.IsCancellationRequested)
                            {
                                try { operation.Port.DiscardInBuffer(); } catch { }
                                throw new TimeoutException($"Serial response timed out after {ReadTimeoutMs} ms.");
                            }
                        }
                    }

                    // 配置了结束符 → 循环读取直到收到完整响应
                    using (var overallCts = CancellationTokenSource.CreateLinkedTokenSource(operation.TokenSource.Token))
                    {
                        overallCts.CancelAfter(OverallReadTimeoutMs);

                        using (var ms = new MemoryStream())
                        {
                            var buffer = new byte[Math.Min(1024, bufferSize)];

                            while (true)
                            {
                                int len;
                                try
                                {
                                    len = await operation.Port.BaseStream.ReadAsync(
                                        buffer, 0, buffer.Length, overallCts.Token).ConfigureAwait(false);
                                }
                                catch (OperationCanceledException) when (
                                    !ct.IsCancellationRequested && !operation.TokenSource.IsCancellationRequested)
                                {
                                    try { operation.Port.DiscardInBuffer(); } catch { }
                                    throw new TimeoutException(
                                        $"Serial response timed out after {OverallReadTimeoutMs} ms. " +
                                        $"Received {ms.Length} byte(s) before timeout.");
                                }

                                if (len > 0)
                                    ms.Write(buffer, 0, len);

                                // 检查是否已收到结束符
                                if (ContainsTerminator(ms.GetBuffer(), (int)ms.Length, ResponseTerminator))
                                    return ms.ToArray();

                                // 防止响应无限增长
                                if (ms.Length >= bufferSize)
                                {
                                    try { operation.Port.DiscardInBuffer(); } catch { }
                                    throw new InvalidOperationException(
                                        $"Response exceeded buffer size {bufferSize} without terminator.");
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                _ioLock.Release();
            }
        }

        private static bool ContainsTerminator(byte[] data, int length, byte[] terminator)
        {
            if (length < terminator.Length) return false;
            for (int i = 0; i <= length - terminator.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < terminator.Length; j++)
                {
                    if (data[i + j] != terminator[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return true;
            }
            return false;
        }



        //public async Task<byte[]> SendReceiveAsync(byte[] request, int bufferSize = 4096, CancellationToken ct = default)
        //{
        //    if (request == null || request.Length == 0)
        //        throw new ArgumentException("Request cannot be empty.", nameof(request));
        //    if (bufferSize <= 0)
        //        throw new ArgumentOutOfRangeException(nameof(bufferSize));

        //    await _ioLock.WaitAsync(ct).ConfigureAwait(false);
        //    try
        //    {
        //        var operation = BeginOperation(ct);
        //        using (operation.TokenSource)
        //        using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(operation.TokenSource.Token))
        //        {
        //            // 发送请求（带写超时）
        //            using (var writeCts = CancellationTokenSource.CreateLinkedTokenSource(operation.TokenSource.Token))
        //            {
        //                if (WriteTimeoutMs > 0)
        //                    writeCts.CancelAfter(WriteTimeoutMs);
        //                try
        //                {
        //                    await operation.Port.BaseStream.WriteAsync(
        //                        request, 0, request.Length, writeCts.Token).ConfigureAwait(false);
        //                }
        //                catch (OperationCanceledException) when (!ct.IsCancellationRequested && !operation.TokenSource.IsCancellationRequested)
        //                {
        //                    throw new TimeoutException($"Serial write timed out after {WriteTimeoutMs} ms.");
        //                }
        //            }

        //            await Task.Delay(100, operation.TokenSource.Token).ConfigureAwait(false);

        //            timeoutCts.CancelAfter(ReadTimeoutMs);
        //            var buffer = new byte[bufferSize];
        //            try
        //            {
        //                var len = await operation.Port.BaseStream.ReadAsync(
        //                    buffer, 0, buffer.Length, timeoutCts.Token).ConfigureAwait(false);
        //                return buffer[..len];
        //            }
        //            catch (OperationCanceledException) when (
        //                !ct.IsCancellationRequested && !operation.TokenSource.IsCancellationRequested)
        //            {
        //                // ★ 超时后清理输入缓冲区：Windows 下 ReadAsync 的取消
        //                //   不一定撤销原生 ReadFile IRP，残留数据可能被下次读取误取
        //                try { operation.Port.DiscardInBuffer(); } catch { }
        //                throw new TimeoutException($"Serial response timed out after {ReadTimeoutMs} ms.");
        //            }
        //        }
        //    }
        //    finally
        //    {
        //        _ioLock.Release();
        //    }
        //}

        private (SerialPort Port, CancellationTokenSource TokenSource) BeginOperation(CancellationToken callerToken)
        {
            lock (_stateLock)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(SerialTransport));
                if (_port == null || !_port.IsOpen || _connectionCts == null)
                    throw new InvalidOperationException("Serial port is not open.");
                return (
                    _port,
                    CancellationTokenSource.CreateLinkedTokenSource(callerToken, _connectionCts.Token));
            }
        }

        public void Dispose()
        {
            bool shouldClose;
            lock (_stateLock)
            {
                shouldClose = !_disposed;
                _disposed = true;
            }
            if (!shouldClose) return;

            try
            {
                CloseAsync(CancellationToken.None).Wait(TimeSpan.FromSeconds(5));
            }
            catch { }
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
