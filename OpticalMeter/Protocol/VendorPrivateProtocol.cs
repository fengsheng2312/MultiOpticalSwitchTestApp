using OpticalMeter.Contract;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpticalMeter.Protocol
{
    /// <summary>
    /// 厂商私有协议（示例）
    /// 帧格式: [0xAA] [0x55] [命令码] [数据长度] [数据...] [校验和]
    /// </summary>
    public class VendorPrivateProtocol : IProtocol
    {
        public string Name => "VendorPrivate";

        // 帧头
        private const byte FRAME_HEAD_1 = 0xAA;
        private const byte FRAME_HEAD_2 = 0x55;

        // 命令码
        private const byte CMD_READ_POWER = 0x01;
        private const byte CMD_SET_WAVELENGTH = 0x02;
        private const byte CMD_GET_WAVELENGTH = 0x03;
        private const byte CMD_ZERO = 0x04;

        public byte[] Encode(string command, Dictionary<string, string> parameters)
        {
            var cmdByte = command switch
            {
                "readPower" => CMD_READ_POWER,
                "setWavelength" => CMD_SET_WAVELENGTH,
                "getWavelength" => CMD_GET_WAVELENGTH,
                "zero" => CMD_ZERO,
                _ => throw new NotSupportedException($"不支持的命令: {command}")
            };

            // 构造数据段
            var data = Array.Empty<byte>();
            if (parameters != null && parameters.TryGetValue("value", out var valueStr))
            {
                if (double.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    var intValue = (ushort)(value * 10);
                    data = new byte[] { (byte)(intValue >> 8), (byte)(intValue & 0xFF) };
                }
            }

            // 构造完整帧: [帧头2字节] [命令码1字节] [数据长度1字节] [数据N字节] [校验和1字节]
            var frame = new byte[4 + data.Length + 1];
            frame[0] = FRAME_HEAD_1;
            frame[1] = FRAME_HEAD_2;
            frame[2] = cmdByte;
            frame[3] = (byte)data.Length;
            Array.Copy(data, 0, frame, 4, data.Length);
            frame[frame.Length - 1] = CalcChecksum(frame, frame.Length - 1);
            return frame;
        }

        public string Decode(byte[] response)
        {
            if (response == null || response.Length < 5)
                throw new InvalidOperationException("响应帧太短");

            if (response[0] != FRAME_HEAD_1 || response[1] != FRAME_HEAD_2)
                throw new InvalidOperationException("帧头不匹配");

            var dataLength = response[3];
            if (response.Length < 4 + dataLength + 1)
                throw new InvalidOperationException("响应帧长度不足");

            // 校验
            var checksum = CalcChecksum(response, response.Length - 1);
            if (checksum != response[response.Length - 1])
                throw new InvalidOperationException("校验和错误");

            // 提取数据段
            var data = response.Skip(4).Take(dataLength).ToArray();

            // 解析数值（大端序）
            if (dataLength >= 4)
            {
                var value = (data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3];
                return ((float)value / 10000.0).ToString("F4", CultureInfo.InvariantCulture);
            }

            if (dataLength >= 2)
            {
                var value = (data[0] << 8) | data[1];
                return (value / 10.0).ToString("F1", CultureInfo.InvariantCulture);
            }

            return BitConverter.ToString(data);
        }

        /// <summary>
        /// 异或校验
        /// </summary>
        private static byte CalcChecksum(byte[] data, int length)
        {
            byte sum = 0;
            for (int i = 0; i < length; i++)
                sum ^= data[i];
            return sum;
        }
    }
}