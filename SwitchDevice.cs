namespace MultiOpticalSwitchTestApp.Device
{
    /// <summary>光开关业务设备，继承基类，后续可以扩展专属业务方法</summary>
    public class SwitchDevice : DeviceBase
    {
        // 这里可以写仪器专属业务方法，屏蔽SCPI指令
        // public async Task SetChannel(int ch)
        // {
        //     await WriteAsync($":SWITCH:CHAN {ch}");
        // }
        // public async Task<int> GetChannel()
        // {
        //     var ret = await QueryAsync(":SWITCH:CHAN?");
        //     return int.Parse(ret);
        // }
        public async Task SwitchLightChannel(int channel)
        {
            if (channel < 1 || channel > 8)
                throw new ArgumentOutOfRangeException(nameof(channel), "通道必须 1~8");

            byte[] cmd = new byte[] { 0x5A, 0xA5, 0x01, (byte)channel };
            await WriteBytesAsync(cmd);
        }
    }
}
