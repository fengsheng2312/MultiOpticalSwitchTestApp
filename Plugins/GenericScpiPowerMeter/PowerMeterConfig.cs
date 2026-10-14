using System.Collections.Generic;
using System.Net;

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

        public List<TransportType>? SupportedTransports { get; set; } = [];

        /// <summary>串口参数（传输方式为 Serial 时使用）</summary>
        public SerialSettings SerialSettings { get; set; } = new SerialSettings();


        /// <summary>SCPI 命令集合</summary>
        public Command Commands { get; set; } = new Command();

        /// <summary>
        /// 连接成功后下发一次的模式切换命令（协议文档 SENSe:POWer:MODe：0 标准 / 1 高速 / 2 飞速）；
        /// 留空则不下发。高速模式下配合查询式读取（如 READ2:SCALar:POWer:DC?）刷新率高于标准模式。
        /// </summary>
        public string? HighSpeedModeCommand { get; set; }

        /// <summary>
        /// 飞速流模式配置（可选）。未配置该节表示该型号不支持流模式，
        /// 轮询等调用方会自动回退到普通查询式读取。
        /// </summary>
        public TurboConfig? Turbo { get; set; }

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

    /// <summary>
    /// 飞速流模式配置：设备持续主动吐定长帧，上位机组帧缓存各通道最新值。
    /// </summary>
    public class TurboConfig
    {
        /// <summary>是否启用飞速流模式（false 时该型号仍走查询式读取）</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>开启流模式的十六进制帧（如 "EE AA 60 00 00 00"），空格/连字符可选</summary>
        public string StartFrame { get; set; } = "";

        /// <summary>退出流模式的帧（可选）；留空则停止采集时只本地停循环、不向设备下发</summary>
        public string StopFrame { get; set; } = "";

        /// <summary>帧头字节，用于组帧同步（双通道实测为 0x00）</summary>
        public byte FrameHeader { get; set; } = 0x00;

        /// <summary>通道数（四通道设备填 4）</summary>
        public int ChannelCount { get; set; } = 4;

        /// <summary>每通道字节数（实测 2）</summary>
        public int BytesPerChannel { get; set; } = 2;

        /// <summary>每通道数值是否为有符号（补码）；若负功率读不出来可置 true</summary>
        public bool Signed { get; set; }

        /// <summary>
        /// 数值编码："Int"（含旧值 "Int16LE"/"Int16BE"）=定长整数，字节序由 ByteOrder 决定；
        /// "IntFrac"=字节1整数部分(sbyte)+字节2小数部分(÷FractionDivisor)。
        /// </summary>
        public string Encoding { get; set; } = "Int16LE";

        /// <summary>
        /// 整数字节序："Little"（默认，低字节在前）/ "Big"（高字节在前）。
        /// 四通道飞速流实测为大端，如 15 1D → 0x151D = 5405。
        /// </summary>
        public string ByteOrder { get; set; } = "Little";

        /// <summary>IntFrac 模式下小数字节的分度（100 → 0.01dBm/LSB）</summary>
        public double FractionDivisor { get; set; } = 100.0;

        /// <summary>
        /// 原始码值零点：dBm = (原始码值 − RawOffset) / RawScale × Scale + Offset。
        /// 四通道飞速流实测为 9000。
        /// </summary>
        public double RawOffset { get; set; }

        /// <summary>
        /// 原始码值分度（每 dBm 对应多少码值）：如 83.84 表示 83.84 码值 = 1 dB。
        /// 0 视同 1，避免配置错误导致除零。
        /// </summary>
        public double RawScale { get; set; } = 1.0;

        /// <summary>原始整数 → dBm 的缩放系数（如 0.01 表示原始值以 0.01dB 为单位）</summary>
        public double Scale { get; set; } = 1.0;

        /// <summary>缩放后再叠加的偏移量（dB）</summary>
        public double Offset { get; set; }

        /// <summary>
        /// 开启流模式后等首帧的最长时间（毫秒）。轮询启动瞬间设备可能还没吐帧，
        /// 此时按该时长等待而不是立即失败中断整轮轮询；≤0 时取默认 800ms。
        /// </summary>
        public int FirstFrameTimeoutMs { get; set; } = 800;
    }
}
