using Free_spaceOpticalCirculatorTestApp.ComunicationTransport;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;

namespace Free_spaceOpticalCirculatorTestApp
{
    /// <summary>
    /// 全局异步串口管理器，统一管理多路AsyncSerialPort实例，硬件层，只认COM端口
    /// </summary>
    public static class SerialGlobalManager
    {
        private static readonly object _lockObj = new();
        private static readonly Dictionary<string, AsyncSerialPort> _serialPorts = new();

        /// <summary>获取串口实例，不存在返回null</summary>
        public static AsyncSerialPort? GetPort(string portName)
        {
            lock (_lockObj)
            {
                _serialPorts.TryGetValue(portName, out var port);
                return port;
            }
        }

        /// <summary>外部手动new AsyncSerialPort后加入管理器</summary>
        public static bool TryAddPort(string portName, AsyncSerialPort serial)
        {
            if (serial == null) throw new ArgumentNullException(nameof(serial));
            lock (_lockObj)
            {
                if (_serialPorts.ContainsKey(portName))
                    return false;

                BindPortEvents(portName, serial);
                _serialPorts.Add(portName, serial);
                return true;
            }
        }

        /// <summary>打开串口，内部创建AsyncSerialPort实例</summary>
        public static void OpenPort(string portName, int baudRate, int dataBits = 8, Parity parity = Parity.None, StopBits stopBits = StopBits.One)
        {
            lock (_lockObj)
            {
                if (_serialPorts.TryGetValue(portName, out var oldPort))
                {
                    oldPort.Dispose();
                    _serialPorts.Remove(portName);
                }

                var sp = new AsyncSerialPort(portName, baudRate, dataBits, parity, stopBits);
                BindPortEvents(portName, sp);
                sp.Open();
                _serialPorts.Add(portName, sp);
            }
        }

        /// <summary>关闭并释放指定端口</summary>
        public static void RemoveAndDisposePort(string portName)
        {
            lock (_lockObj)
            {
                if (!_serialPorts.TryGetValue(portName, out var sp))
                    return;

                sp.Close();
                sp.Dispose();
                _serialPorts.Remove(portName);
            }
        }

        /// <summary>全部释放，程序退出调用</summary>
        public static void DisposeAllPorts()
        {
            lock (_lockObj)
            {
                foreach (var kvp in _serialPorts.Values)
                {
                    kvp.Close();
                    kvp.Dispose();
                }
                _serialPorts.Clear();
            }
        }

        /// <summary>判断端口是否在线打开，供下拉刷新使用</summary>
        public static bool IsPortOnline(string portName)
        {
            lock (_lockObj)
            {
                return _serialPorts.TryGetValue(portName, out var sp) && sp.IsOpen;
            }
        }

        /// <summary>获取所有已打开COM</summary>
        public static List<string> GetAllOpenedCom()
        {
            lock (_lockObj)
            {
                var list = new List<string>();
                foreach (var kv in _serialPorts)
                {
                    if (kv.Value.IsOpen) list.Add(kv.Key);
                }
                return list;
            }
        }

        /// <summary>SCPI查询，硬件层接口，传入COM端口</summary>
        /// 一问一答，SCPI常用
        public static async Task<string> QueryAsync(string comPort, string cmd, int timeoutMs = 2000)
        {
            AsyncSerialPort? port;
            lock (_lockObj)
            {
                if (!_serialPorts.TryGetValue(comPort, out port) || !port.IsOpen)
                    throw new InvalidOperationException($"串口 {comPort} 未打开");
            }
            // 发送日志
            SerialLogEvent?.Invoke(true, cmd);
            string resp = await port.QueryAsync(cmd, timeoutMs);
            // 接收应答日志
            SerialLogEvent?.Invoke(false, resp);
            return resp;
        }

        //只管发不等消息回复
        /// <summary>SCPI发送，硬件层接口，传入COM端口</summary>
        /// <summary>SCPI发送，硬件层接口，传入COM端口</summary>
        public static async Task WriteAsync(string comPort, string cmd)
        {
            AsyncSerialPort? port;
            lock (_lockObj)
            {
                if (!_serialPorts.TryGetValue(comPort, out port) || !port.IsOpen)
                    throw new InvalidOperationException($"串口 {comPort} 未打开");
            }
            await port.WriteAsync(cmd);
            // 触发发送日志回调
            SerialLogEvent?.Invoke(true, cmd);
        }
        /// <summary>发送原始二进制字节数组（私有二进制协议，光开关）</summary>
        public static async Task WriteBytesAsync(string comPort, byte[] data)
        {
            AsyncSerialPort? port;
            lock (_lockObj)
            {
                if (!_serialPorts.TryGetValue(comPort, out port) || !port.IsOpen)
                    throw new InvalidOperationException($"串口 {comPort} 未打开");
            }
            Console.WriteLine($">>>真实调用底层发送，长度={data.Length}");
            await port.WriteBytesAsync(data);
            string hexStr = BitConverter.ToString(data).Replace("-", " ");
            SerialLogEvent?.Invoke(true, $"[BIN] {hexStr}");
        }


        #region 全局转发事件，携带COM端口
        public static event Action<string, string>? OnPortDataReceived;
        public static event Action<string>? OnPortDisconnected;
        public static event Action<string, Exception>? OnPortError;
        #endregion
        public static event Action<bool, string>? SerialLogEvent;
        public static event Action<string, byte[]> OnPortRawDataReceived;
        #region 内部事件绑定
        private static void BindPortEvents(string com, AsyncSerialPort port)
        {
            port.OnDataReceived += data => OnPortDataReceived?.Invoke(com, data);

            // ==========新增原始字节接收绑定==========
            port.OnRawDataReceived += rawBytes =>
            {
                Console.WriteLine($"[SerialGlobalManager] RawData COM={com} : {BitConverter.ToString(rawBytes)}");
                string hex = BitConverter.ToString(rawBytes).Replace("-", " ");
                //SerialLogEvent?.Invoke(false, $"RAW:{hex}");
                // 向外抛出原始字节给上层窗体
                OnPortRawDataReceived?.Invoke(com, rawBytes);
            };

            port.OnDisconnected += () =>
            {
                Console.WriteLine($"[SerialGlobalManager] ⚠️底层触发OnDisconnected COM={com}");
                RemoveAndDisposePort(com);
                OnPortDisconnected?.Invoke(com);
            };
            port.OnError += ex =>
            {
                Console.WriteLine($"[SerialGlobalManager] ❌COM={com}发生异常：{ex.Message}");
                SerialLogEvent?.Invoke(false, $"[ERROR] {ex.Message}");
                OnPortError?.Invoke(com, ex);
            };
        }


        #endregion
    }
}
