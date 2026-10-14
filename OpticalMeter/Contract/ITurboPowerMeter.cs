using System.Threading.Tasks;

namespace OpticalMeter.Contract
{
    /// <summary>
    /// 支持"飞速流模式"的功率计：开启后设备不再等查询，而是持续吐二进制帧；
    /// 实现方在后台组帧缓存各通道最新值，ReadPowerAsync 直接返回缓存值。
    /// 不具备该能力的型号不实现此接口，调用方自动回退到普通查询式读取。
    /// </summary>
    public interface ITurboPowerMeter
    {
        /// <summary>飞速流模式当前是否已开启。</summary>
        bool IsTurboActive { get; }

        /// <summary>下发开启帧并启动后台采集循环。</summary>
        Task StartTurboAsync();

        /// <summary>停止后台采集循环。</summary>
        Task StopTurboAsync();
    }
}
