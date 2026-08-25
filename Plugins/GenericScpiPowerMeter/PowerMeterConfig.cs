using System.Collections.Generic;

namespace Plugins.GenericScpiPowerMeter
{
    /// <summary>
    /// 功率计配置模型 —— 对应一个 JSON 配置文件
    /// </summary>
    public class PowerMeterConfig
    {
        /// <summary>厂商名称</summary>
        public string Vendor { get; set; } = "";

        /// <summary>设备型号</summary>
        public string Model { get; set; } = "";

        /// <summary>使用的协议（如 "SCPI"）</summary>
        public string Protocol { get; set; } = "SCPI";

        /// <summary>默认传输方式（如 "Serial" / "TCP"）</summary>
        public string DefaultTransport { get; set; } = "Serial";

        /// <summary>串口参数（传输方式为 Serial 时使用）</summary>
        public SerialSettings SerialSettings { get; set; } = new SerialSettings();

        /// <summary>SCPI 命令集合</summary>
        public Command Commands { get; set; } = new Command();

        /// <summary>响应解析规则</summary>
        public ResponseParserConfig ResponseParser { get; set; } = new ResponseParserConfig();

        /// <summary>波长单位转换系数（nm → 设备单位），默认 1.0 表示设备直接用 nm</summary>
        public double WavelengthScale { get; set; } = 1.0;

        /// <summary>功率单位转换系数，默认 1.0 表示设备直接返回 dBm</summary>
        public double PowerScale { get; set; } = 1.0;

        /// <summary>功率偏移量（dB），最终功率 = 原始值 × PowerScale + PowerOffset</summary>
        public double PowerOffset { get; set; } = 0.0;

        /// <summary>读取超时时间（毫秒）</summary>
        public int ReadTimeoutMs { get; set; } = 3000;

        /// <summary>额外参数（可扩展）</summary>
        public Dictionary<string, string> Extra { get; set; } = new();
    }

    /// <summary>
    /// 串口配置
    /// </summary>
    public class SerialSettings
    {
        /// <summary>波特率</summary>
        public int BaudRate { get; set; } = 9600;

        /// <summary>数据位</summary>
        public int DataBits { get; set; } = 8;

        /// <summary>停止位（字符串："One" / "Two" / "OnePointFive"）</summary>
        public string StopBits { get; set; } = "One";

        /// <summary>校验位（字符串："None" / "Odd" / "Even" / "Mark" / "Space"）</summary>
        public string Parity { get; set; } = "None";

        /// <summary>流控（字符串："None" / "XOnXOff" / "RequestToSend" / "RequestToSendXOnXOff"）</summary>
        public string Handshake { get; set; } = "None";
    }

    /// <summary>
    /// SCPI 命令模板集合
    /// </summary>
    public class Command
    {
        /// <summary>读取功率命令</summary>
        public string ReadPower { get; set; } = "MEAS:POW?";

        /// <summary>设置波长命令，{value} 为占位符</summary>
        public string SetWavelength { get; set; } = "SENS:WAV {value}";

        /// <summary>读取波长命令</summary>
        public string GetWavelength { get; set; } = "SENS:WAV?";

        /// <summary>归零校准命令</summary>
        public string Zero { get; set; } = "SENS:CORR:ACQ";

        /// <summary>设备识别命令</summary>
        public string GetIdn { get; set; } = "*IDN?";
    }

    /// <summary>
    /// 响应解析配置
    /// </summary>
    public class ResponseParserConfig
    {
        /// <summary>读功率响应解析规则</summary>
        public ParseRule ReadPower { get; set; } = new ParseRule();
    }

    /// <summary>
    /// 单条解析规则
    /// </summary>
    public class ParseRule
    {
        /// <summary>解析格式（"Numeric" / "String" / "Custom"）</summary>
        public string Format { get; set; } = "Numeric";

        /// <summary>单位（如 "dBm" / "W"）</summary>
        public string Unit { get; set; } = "dBm";
    }
}
