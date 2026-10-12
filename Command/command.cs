using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;

namespace MultiOpticalSwitchTestApp.Command
{
    public class command
    {
        //单切：5AA50101 ~ 08  ，8个机械光开关状态，每次可以改变一种状态
        //命令是十六进制 5AA501xx  0x01~0x08  代表8个机械光开关的状态，1为闭合，0为断开
        //5AA50101 假如是P1 那5AA50102就是P2  发相应的命令 对应的就导通
        //多开关版：帧格式 5A A5 {id} {channel}，id=开关编号(SwitchCh.json 1~5)，channel=通道号

        /// <summary>
        /// 生成单切指令帧：5A A5 {id} {channel}
        /// </summary>
        /// <param name="id">开关编号（Config/SwitchCh.json 里的 id，1~5）</param>
        /// <param name="channel">通道号（channels 数组元素，1~8）</param>
        public byte[] SwitchLightChannel(int id, int channel)
        {
            if (id < 1 || id > 5)
                throw new ArgumentOutOfRangeException(nameof(id), "开关编号必须 1~5");
            if (channel < 1 || channel > 8)
                throw new ArgumentOutOfRangeException(nameof(channel), "通道必须 1~8");
            return new byte[] { 0x5A, 0xA5, (byte)id, (byte)channel };
        }
    }
}
