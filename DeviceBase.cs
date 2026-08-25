using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Free_spaceOpticalCirculatorTestApp.Device
{
    /// <summary>所有SCPI串口设备基类</summary>
    public abstract class DeviceBase
    {
        /// <summary>绑定的COM端口，从配置/UI赋值</summary>
        public string ComPort { get; set; } = string.Empty;

        private readonly List<byte> _rawRecvBuffer = new List<byte>();
        /// <summary>组装完成一帧完整应答（已剔除 \r\n）</summary>
        public event Action<byte[]>? OnDeviceFrameComplete;

        /// <summary>波特率</summary>
        public int BaudRate { get; set; } = 115200;

        /// <summary>设备是否打开</summary>
        public bool IsOpened => SerialGlobalManager.IsPortOnline(ComPort);

        /// <summary>设备收到报文事件</summary>
        public event Action<string>? OnDataReceived;

        /// <summary>设备断开事件</summary>
        public event Action? OnDisconnected;

        /// <summary>设备通信异常</summary>
        public event Action<Exception>? OnError;

        private bool _isSubscribed;

        /// <summary>打开设备</summary>
        public virtual void Open()
        {
            Console.WriteLine("尝试开串口");
            if (string.IsNullOrWhiteSpace(ComPort))
                throw new InvalidOperationException("未配置设备COM端口");
            Console.WriteLine($"{ComPort}+{BaudRate}");
            try
            {
                SerialGlobalManager.OpenPort(ComPort, BaudRate);
                SubscribeEvents();
                _rawRecvBuffer.Clear(); //打开时清空接收缓存
                Console.WriteLine("✅Open成功");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌Open失败:{ex.Message}");
                throw; //继续往外抛给UI层catch
            }
        }

        /// <summary>关闭设备</summary>
        public virtual void Close()
        {
            UnSubscribeEvents();
            _rawRecvBuffer.Clear(); //关闭清空缓存
            SerialGlobalManager.RemoveAndDisposePort(ComPort);
        }

        /// <summary>SCPI查询</summary>
        public virtual async Task<string> QueryAsync(string cmd, int timeoutMs = 2000)
        {
            if (string.IsNullOrWhiteSpace(ComPort))
                throw new InvalidOperationException("未配置设备COM端口");
            return await SerialGlobalManager.QueryAsync(ComPort, cmd, timeoutMs);
        }

        /// <summary>SCPI发送指令</summary>
        public virtual async Task WriteAsync(string cmd)
        {
            if (string.IsNullOrWhiteSpace(ComPort))
                throw new InvalidOperationException("未配置设备COM端口");
            await SerialGlobalManager.WriteAsync(ComPort, cmd);
        }

        /// <summary>发送原始二进制字节数组，用于私有二进制协议设备（光开关）</summary>
        public virtual async Task WriteBytesAsync(byte[] data)
        {
            if (string.IsNullOrWhiteSpace(ComPort))
                throw new InvalidOperationException("未配置设备COM端口");
            if (data == null || data.Length == 0)
                throw new ArgumentException("发送字节数组不能为空", nameof(data));

            await SerialGlobalManager.WriteBytesAsync(ComPort, data);
        }

        #region 事件订阅管理
        private void SubscribeEvents()
        {
            if (_isSubscribed) return;
            SerialGlobalManager.OnPortDataReceived += GlobalOnDataReceived;
            SerialGlobalManager.OnPortDisconnected += GlobalOnDisconnected;
            SerialGlobalManager.OnPortError += GlobalOnError;
            SerialGlobalManager.OnPortRawDataReceived += GlobalOnRawDataReceived;

            _isSubscribed = true;
        }

        private void UnSubscribeEvents()
        {
            if (!_isSubscribed) return;
            SerialGlobalManager.OnPortDataReceived -= GlobalOnDataReceived;
            SerialGlobalManager.OnPortDisconnected -= GlobalOnDisconnected;
            SerialGlobalManager.OnPortError -= GlobalOnError;
            SerialGlobalManager.OnPortRawDataReceived -= GlobalOnRawDataReceived;

            _isSubscribed = false;
        }
        #endregion

        private void GlobalOnDataReceived(string com, string data)
        {
            if (com == ComPort)
            {
                OnDataReceived?.Invoke(data);
            }
        }

        private void GlobalOnDisconnected(string com)
        {
            if (com == ComPort)
            {
                UnSubscribeEvents();
                OnDisconnected?.Invoke();
            }
        }

        private void GlobalOnError(string com, Exception ex)
        {
            if (com == ComPort)
            {
                OnError?.Invoke(ex);
            }
        }

        /// <summary>原始字节接收回调，做流拼接分包（0D 0A结束符）</summary>
        private void GlobalOnRawDataReceived(string comName, byte[] rawBytes)
        {
            //只处理属于本设备实例的串口，其它COM口直接忽略
            if (comName != ComPort)
                return;

            _rawRecvBuffer.AddRange(rawBytes);

            while (_rawRecvBuffer.Count >= 2)
            {
                int idx = -1;
                for (int i = 0; i < _rawRecvBuffer.Count - 1; i++)
                {
                    if (_rawRecvBuffer[i] == 0x0D && _rawRecvBuffer[i + 1] == 0x0A)
                    {
                        idx = i;
                        break;
                    }
                }
                if (idx == -1)
                    break;

                byte[] frame = _rawRecvBuffer.Take(idx).ToArray();
                _rawRecvBuffer.RemoveRange(0, idx + 2);

                OnDeviceFrameComplete?.Invoke(frame);
            }
        }
    }
}
