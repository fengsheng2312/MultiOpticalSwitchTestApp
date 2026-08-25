using System;
using System.Threading.Tasks;

namespace OpticalMeter.Contract
{
    /// <summary>
    /// 功率计设备接口 —— 主程序只认识这个
    /// </summary>
    public interface IPowerMeter : IDisposable
    {
        /// <summary>设备信息</summary>
        DeviceInfo Info { get; }

        /// <summary>是否已连接</summary>
        bool IsConnected { get; }

        /// <summary>连接设备</summary>
        Task ConnectAsync(string connectionString);

        /// <summary>断开设备</summary>f
        Task DisconnectAsync();

        /// <summary>读取光功率（单位 dBm）</summary>
        Task<double> ReadPowerAsync();

        /// <summary>设置波长（单位 nm）</summary>
        Task SetWavelengthAsync(double wavelengthNm);

        /// <summary>读取当前波长（单位 nm）</summary>
        Task<double> GetWavelengthAsync();

        /// <summary>归零校准</summary>
        Task ZeroAsync();
    }
}