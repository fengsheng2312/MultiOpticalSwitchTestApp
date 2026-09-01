using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpticalMeter.Contract
{
    /// <summary>
    /// 传输层 —— 只负责原始字节的收发，不关心内容含义
    /// </summary>
    
    public interface ITransport : IDisposable
    {
        /// <summary>是否已连接</summary>
        bool IsOpen { get; }

        public int ExpectedResponseLength { get; set; }
        public byte[]? ResponseTerminator { get; set; }
        /// <summary>打开连接</summary>
        Task OpenAsync(string connectionString, CancellationToken ct = default);

        /// <summary>关闭连接</summary>
        Task CloseAsync(CancellationToken ct = default);

        /// <summary>发送数据</summary>
        Task SendAsync(byte[] data, CancellationToken ct = default);

        /// <summary>接收数据</summary>
        Task<byte[]> ReceiveAsync(int bufferSize = 4096, CancellationToken ct = default);

        /// <summary>发送并等待响应（适用于请求-响应模式）</summary>
        Task<byte[]> SendReceiveAsync(byte[] request, int bufferSize = 4096, CancellationToken ct = default);
    }
}