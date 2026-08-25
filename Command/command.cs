using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;

namespace Free_spaceOpticalCirculatorTestApp.Command
{
    public class command
    {
        //单切：5AA50101 ~08  ，8个机械光开关状态，每次可以改变一种状态
        //命令是十六进制 5AA501xx  0x01~0x08  代表8个机械光开关的状态，1为闭合，0为断开
        //5AA50101 假如是P1 那5AA50102就是P2  发相应的命令 对应的就导通
        //public byte[] SwitchLightChannel(int channel)
        //{
        //    if (channel < 1 || channel > 8)
        //        throw new ArgumentOutOfRangeException(nameof(channel), "通道必须 1~8");
        //    byte[] cmd = new byte[] { 0x5A, 0xA5, 0x01, (byte)channel };
        //    return cmd;
        //}
    }
}
