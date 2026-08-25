using System.Collections.Generic;

namespace OpticalMeter.Contract
{
    /// <summary>
    /// 协议层 —— 负责"语义命令"与"协议字节帧"之间的双向翻译
    /// </summary>
    public interface IProtocol
    {
        /// <summary>协议名称（如 "SCPI", "VendorPrivate", "Modbus"）</summary>
        string Name { get; }

        /// <summary>语义命令 → 协议字节帧</summary>
        byte[] Encode(string command, Dictionary<string, string> parameters);

        /// <summary>协议字节帧 → 语义结果字符串</summary>
        string Decode(byte[] response);
    }
}