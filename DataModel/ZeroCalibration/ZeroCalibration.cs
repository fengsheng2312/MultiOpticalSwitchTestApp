using System;
using System.Collections.Generic;

namespace MultiOpticalSwitchTestApp.DataModel.ZeroCalibration
{
    /// <summary>
    /// 单条归零记录
    /// </summary>
    public class ZeroCalibration
    {
        /// <summary>测试项编号 1~7</summary>
        public int TestItemId { get; set; }

        /// <summary>测试项名称，如 "Tx‑Com(IL)"</summary>
        public string TestItemName { get; set; } = string.Empty;

        /// <summary>功率计通道 CH1 / CH2</summary>
        public string Channel { get; set; } = string.Empty;

        /// <summary>归零值（dBm），带符号</summary>
        public double ZeroValueDbm { get; set; }

        /// <summary>归零时间</summary>
        public DateTime CalibratedAt { get; set; }

        /// <summary>开关状态数组</summary>
        public List<string> SWStatus { get; set; } = new();
    }
}
