using System;
using System.Collections.Generic;

namespace Free_spaceOpticalCirculatorTestApp.DataModel.ZeroCalibration
{
    /// <summary>
    /// 单台测试盒归零数据集
    /// </summary>
    public class TestBoxZeroEntry
    {
        /// <summary>测试盒SN</summary>
        public string TestBoxSn { get; set; } = string.Empty;

        /// <summary>该盒子最后一次归零更新时间</summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>该盒子全部归零项</summary>
        public List<ZeroCalibration> Items { get; set; } = new();
    }

    /// <summary>
    /// 归零配置文件根对象，支持多台测试盒共存
    /// </summary>
    public class ZeroConfig
    {
        /// <summary>配置版本，方便以后升级兼容</summary>
        public int Version { get; set; } = 1;

        /// <summary>全局文件最后修改时间</summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>多台测试盒集合，每一个SN对应一组归零数据</summary>
        public List<TestBoxZeroEntry> TestBoxes { get; set; } = new();
    }
}
