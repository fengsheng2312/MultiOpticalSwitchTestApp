using OpticalMeter.Contract;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpticalMeter.Protocol
{
    /// <summary>
    /// SCPI 标准文本协议
    /// 编码: 语义命令 + 参数替换 + "\n" → ASCII bytes
    /// 解码: ASCII bytes → trim 后的字符串
    /// </summary>
    public class ScpiProtocol : IProtocol
    {
        public string Name => "SCPI";

        public byte[] Encode(string command, Dictionary<string, string> parameters)
        {
            var cmd = command;

            // 替换参数占位符，如 {channel} → "1"
            if (parameters != null)
            {
                foreach (var kv in parameters)
                {
                    cmd = cmd.Replace($"{{{kv.Key}}}", kv.Value);
                }
            }

            // 移除未被替换的占位符（可选参数）
            // 例如 ":MEAS:POW?{channel}" 中 channel 未提供时，变成 ":MEAS:POW?"
            cmd = System.Text.RegularExpressions.Regex.Replace(cmd, @"\{[^}]+\}", "");

            return Encoding.ASCII.GetBytes(cmd + "\n");
        }

        public string Decode(byte[] response)
        {
            return Encoding.ASCII.GetString(response).Trim();
        }
    }
}