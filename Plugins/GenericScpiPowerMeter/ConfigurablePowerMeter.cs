using MultiOpticalSwitchTestApp;
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
    public class ConfigurablePowerMeter : IPowerMeter, ITurboPowerMeter
    {
        private  ITransport _transport;
        private  IProtocol _protocol;
        private  PowerMeterConfig _config;
        private bool _disposed;

        // ===== 飞速流模式状态 =====
        private CancellationTokenSource? _turboCts;
        private Task? _turboLoop;
        private readonly object _frameLock = new();
        private readonly double[] _channelValues = new double[8];
        private readonly bool[] _channelValid = new bool[8];
        private long _turboFrameCount;          // 已成功入缓存的帧数，用于无数据时定位问题
        private long _turboDroppedCount;        // 因合理性校验未通过而丢弃的候选帧数
        private DateTime _turboStartedUtc;

        public DeviceInfo Info { get; set; }
        public bool IsConnected => _transport.IsOpen;
        public bool IsTurboActive => _turboLoop is { IsCompleted: false };

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

            // 连接成功后把设备切到高速模式（协议文档 SENSe:POWer:MODe：0 标准 / 1 高速 / 2 飞速），
            // 之后读功率走查询式指令（如 READ2:SCALar:POWer:DC?），通道由 Global.readChannel 决定
            if (!string.IsNullOrWhiteSpace(_config.HighSpeedModeCommand))
            {
                try
                {
                    var modeBytes = _protocol.Encode(_config.HighSpeedModeCommand, null);
                    Console.WriteLine($"[Mode] 发送模式切换命令: {BitConverter.ToString(modeBytes)}");
                    var modeResponse = await _transport.SendReceiveAsync(modeBytes);
                    Console.WriteLine($"[Mode] 设备响应: {_protocol.Decode(modeResponse)}");
                }
                catch (Exception ex)
                {
                    // 模式切换失败不影响连接，设备仍可停留在标准模式读取
                    Console.WriteLine($"[Mode] 模式切换命令失败（不影响连接）: {ex.Message}");
                }
            }
        }

        public async Task DisconnectAsync()
        {
            // 端口关闭前先停采集循环，避免后台读操作与关闭句柄竞态
            await StopTurboAsync().ConfigureAwait(false);
            await _transport.CloseAsync();
        }

        public async Task<double> ReadPowerAsync()
        {   
            // 飞速流模式：直接取后台组帧缓存的当前通道值，不再发查询指令
            if (_config.Turbo is { Enabled: true } turboCfg && IsTurboActive)
            {
                if (!int.TryParse(Global.readChannel?.Trim(), out int ch) || ch < 1)
                    throw new InvalidOperationException(
                        $"飞速模式无法识别读取通道 \"{Global.readChannel}\"，应为 1~{turboCfg.ChannelCount}");

                if (await TryGetTurboValueAsync(turboCfg, ch).ConfigureAwait(false) is double turboPower)
                    return turboPower;

                throw new InvalidOperationException(BuildTurboWaitFailureMessage(ch, turboCfg));
            }

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

        #region 飞速流模式（后台组帧 + 缓存各通道最新值）

        /// <summary>下发开启帧并启动后台采集循环。</summary>
        public async Task StartTurboAsync()
        {
            var t = _config.Turbo;
            if (t is null || !t.Enabled)
                throw new InvalidOperationException($"{_config.Model} 配置文件未启用飞速流模式（Turbo 节）");
            if (IsTurboActive) return;

            byte[] start = ParseHexFrame(t.StartFrame);
            if (start.Length == 0)
                throw new InvalidOperationException("Turbo.StartFrame 未配置或不是合法十六进制串");

            lock (_frameLock)
            {
                Array.Clear(_channelValid, 0, _channelValid.Length);
                _turboFrameCount = 0;
                _turboDroppedCount = 0;
            }
            _turboStartedUtc = DateTime.UtcNow;

            await _transport.SendAsync(start).ConfigureAwait(false);
            Console.WriteLine($"[Turbo] {_config.Model} 已开启飞速模式: {BitConverter.ToString(start)}");

            var cts = new CancellationTokenSource();
            _turboCts = cts;
            _turboLoop = Task.Run(() => TurboLoopAsync(t, cts.Token));
        }

        /// <summary>停止后台采集循环；配置了 StopFrame 时同步下发退出帧。</summary>
        public async Task StopTurboAsync()
        {
            var loop = _turboLoop;
            var cts = _turboCts;
            _turboLoop = null;
            _turboCts = null;

            if (cts != null)
            {
                cts.Cancel();
                if (loop != null)
                {
                    try { await loop.ConfigureAwait(false); }
                    catch { /* 取消引发的异常属正常退出 */ }
                }
                cts.Dispose();
            }

            var t = _config.Turbo;
            if (t != null)
            {
                byte[] stop = ParseHexFrame(t.StopFrame);
                if (stop.Length > 0)
                {
                    try
                    {
                        await _transport.SendAsync(stop).ConfigureAwait(false);
                        Console.WriteLine($"[Turbo] {_config.Model} 已下发退出帧: {BitConverter.ToString(stop)}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Turbo] 退出帧发送失败: {ex.Message}");
                    }
                }
            }

            lock (_frameLock)
            {
                Array.Clear(_channelValid, 0, _channelValid.Length);
            }
        }

        /// <summary>后台组帧循环：持续读原始字节，按帧头与固定帧长切帧并写入缓存。</summary>
        private async Task TurboLoopAsync(TurboConfig t, CancellationToken token)
        {
            int chCount = Math.Max(1, t.ChannelCount);
            int bpc = Math.Max(1, t.BytesPerChannel);
            int frameLen = 1 + chCount * bpc;   // 帧头 1 字节 + 各通道数据
            var buffer = new List<byte>(256);

            while (!token.IsCancellationRequested)
            {
                byte[] chunk;
                try
                {
                    chunk = await _transport.ReceiveAsync(256, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;                       // 正常停止
                }
                catch (TimeoutException)
                {
                    continue;                    // 暂时没数据，继续等
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Turbo] 采集读取结束: {ex.Message}");
                    break;
                }
                if (chunk.Length == 0) continue;

                buffer.AddRange(chunk);

                while (buffer.Count >= frameLen)
                {
                    // 重新同步到帧头；负载里出现同值字节时按下一帧校正
                    int h = buffer.IndexOf(t.FrameHeader);
                    if (h < 0)
                    {
                        // 整段都没有帧头，保留尾部一帧长度以内的数据等后续拼接
                        if (buffer.Count > frameLen) buffer.RemoveRange(0, buffer.Count - frameLen);
                        break;
                    }
                    if (h > 0) buffer.RemoveRange(0, h);
                    if (buffer.Count < frameLen) break;

                    // 下一帧的帧头已经到达时先校验对齐：
                    // 通道原始值里出现与帧头同值的字节（如 0x00）会让同步点落到负载上，
                    // 此时只前移 1 字节重试，而不是把整帧错位数据写进缓存。
                    if (buffer.Count > frameLen && buffer[frameLen] != t.FrameHeader)
                    {
                        buffer.RemoveRange(0, 1);
                        CountDropped();
                        continue;
                    }

                    byte[] frame = buffer.GetRange(0, frameLen).ToArray();
                    buffer.RemoveRange(0, frameLen);
                    StoreFrame(frame, t, chCount, bpc);
                }

                // 兜底防止缓冲无限增长（例如设备已退出流模式但本循环仍在跑）
                if (buffer.Count > frameLen * 8) buffer.RemoveRange(0, buffer.Count - frameLen);
            }
        }

        /// <summary>
        /// 解析一帧各通道功率并写入缓存。
        /// 整数编码：dBm = (原始码值 − RawOffset) ÷ RawScale × Scale + Offset；
        /// 四通道飞速流实测为大端 2 字节、RawOffset=9000、RawScale=83.84，
        /// 例如 15 1D → 0x151D = 5405 → (5405−9000)/83.84 = −42.88 dBm。
        /// </summary>
        private void StoreFrame(byte[] frame, TurboConfig t, int chCount, int bpc)
        {
            bool intFrac = string.Equals(t.Encoding, "IntFrac", StringComparison.OrdinalIgnoreCase);
            // 仅显式配置 "Big" / "BigEndian" 时按大端解析，其余（含缺省）沿用旧的小端行为
            bool bigEndian = string.Equals(t.ByteOrder, "Big", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(t.ByteOrder, "BigEndian", StringComparison.OrdinalIgnoreCase);
            double rawScale = t.RawScale == 0 ? 1.0 : t.RawScale;

            var vals = new double[chCount];
            for (int i = 0; i < chCount; i++)
            {
                int off = 1 + i * bpc;
                if (off + bpc > frame.Length) return;

                if (intFrac)
                {
                    // 字节1=整数部分(dBm，有符号)，字节2=小数部分(÷FractionDivisor，默认 0.01dBm/LSB)
                    // 负功率时小数向更负方向叠加：E2 34 → -30.34，而不是 -29.66
                    double intPart = (sbyte)frame[off];
                    double fracPart = frame[off + 1] / (t.FractionDivisor <= 0 ? 100.0 : t.FractionDivisor);
                    vals[i] = (intPart < 0 ? intPart - fracPart : intPart + fracPart) + t.Offset;
                    continue;
                }

                long raw = 0;
                for (int j = 0; j < bpc; j++)
                {
                    raw = bigEndian
                        ? (raw << 8) | frame[off + j]                 // 高字节在前：15 1D → 0x151D
                        : raw | ((long)frame[off + j] << (8 * j));  // 低字节在前：1D 15 → 0x151D
                }
                if (t.Signed)
                {
                    if (bpc == 1) raw = (sbyte)(raw & 0xFF);
                    else if (bpc == 2) raw = (short)(raw & 0xFFFF);
                    else if (bpc == 4) raw = (int)(raw & 0xFFFFFFFF);
                }

                vals[i] = (raw - t.RawOffset) / rawScale * t.Scale + t.Offset;
            }

            lock (_frameLock)
            {
                for (int i = 0; i < chCount && i < _channelValues.Length; i++)
                {
                    _channelValues[i] = vals[i];
                    _channelValid[i] = true;
                }
                _turboFrameCount++;
            }
        }

        /// <summary>记录一个未能对齐而被丢弃的候选帧，供超时诊断使用。</summary>
        private void CountDropped()
        {
            lock (_frameLock) { _turboDroppedCount++; }
        }

        /// <summary>
        /// 取指定通道（1 起）的最新缓存值；首帧还没到达时最多等 Turbo.FirstFrameTimeoutMs，
        /// 免得轮询刚启动就因一帧未收到而整轮中断。返回 null 表示等到超时仍无有效帧。
        /// </summary>
        private async Task<double?> TryGetTurboValueAsync(TurboConfig t, int channel)
        {
            int chCount = Math.Max(1, t.ChannelCount);
            if (channel < 1 || channel > chCount || channel > _channelValid.Length) return null;

            int waitMs = t.FirstFrameTimeoutMs > 0 ? t.FirstFrameTimeoutMs : 800;
            var deadline = DateTime.UtcNow.AddMilliseconds(waitMs);

            while (true)
            {
                lock (_frameLock)
                {
                    if (_channelValid[channel - 1]) return _channelValues[channel - 1];
                }

                if (!IsTurboActive || DateTime.UtcNow >= deadline) return null;
                await Task.Delay(20).ConfigureAwait(false);
            }
        }

        /// <summary>组帧超时时的诊断信息：区分“设备一帧没吐”“吐了但对不上帧”两种情况。</summary>
        private string BuildTurboWaitFailureMessage(int channel, TurboConfig t)
        {
            long frames, dropped;
            lock (_frameLock) { frames = _turboFrameCount; dropped = _turboDroppedCount; }
            int elapsedMs = (int)(DateTime.UtcNow - _turboStartedUtc).TotalMilliseconds;
            int chCount = Math.Max(1, t.ChannelCount);

            if (channel > chCount)
                return $"通道 {channel} 超出飞速流配置的通道数 {chCount}";

            if (frames == 0 && dropped == 0)
                return $"飞速模式已开启 {elapsedMs}ms，设备未吐出任何数据帧（读取通道 {channel}）；" +
                       "请检查开启帧是否正确、设备是否仍在流模式";

            if (frames == 0)
                return $"飞速模式收到字节但无法对齐数据帧（丢弃 {dropped} 个候选帧，读取通道 {channel}）；" +
                       "请核对 Turbo 节的 FrameHeader / ChannelCount / BytesPerChannel";

            return $"飞速模式暂未收到通道 {channel} 的有效数据帧（已入缓存 {frames} 帧）";
        }

        /// <summary>把 "EE AA 60 00 00 00" 之类文本解析成字节数组。</summary>
        private static byte[] ParseHexFrame(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return Array.Empty<byte>();
            var sb = new System.Text.StringBuilder(hex.Length);
            foreach (char c in hex)
            {
                if (Uri.IsHexDigit(c)) sb.Append(c);
            }
            string digits = sb.ToString();
            if (digits.Length % 2 != 0 || digits.Length == 0) return Array.Empty<byte>();

            var bytes = new byte[digits.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(digits.Substring(i * 2, 2), 16);
            return bytes;
        }

        #endregion

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
                // 先让后台采集循环退出，再释放端口
                try { _turboCts?.Cancel(); } catch { }
                try { _turboCts?.Dispose(); } catch { }
                _turboCts = null;
                _turboLoop = null;
                _transport.Dispose();
            }
        }
    }
}