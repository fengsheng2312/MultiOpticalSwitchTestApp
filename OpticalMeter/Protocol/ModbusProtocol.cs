using OpticalMeter.Contract;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpticalMeter.Protocol
{
    /// <summary>
    /// Modbus RTU 协议（示例）
    /// 帧格式: [设备地址] [功能码] [寄存器地址H] [寄存器地址L] [数据H] [数据L] [CRC_L] [CRC_H]
    /// </summary>
    public class ModbusProtocol : IProtocol
    {
        public string Name => "Modbus";

        private const byte DEVICE_ADDR = 0x01;
        private const byte FUNC_READ_HOLDING = 0x03;
        private const byte FUNC_WRITE_SINGLE = 0x06;

        // 寄存器地址映射（示例）
        private const ushort REG_POWER = 0x0000;
        private const ushort REG_WAVELENGTH = 0x0002;

        public byte[] Encode(string command, Dictionary<string, string> parameters)
        {
            return command switch
            {
                "readPower" => BuildReadHoldingRegister(REG_POWER, 2),

                "setWavelength" => BuildWriteSingleRegister(REG_WAVELENGTH,
                    (ushort)(double.Parse(parameters["value"], CultureInfo.InvariantCulture) * 10)),

                "getWavelength" => BuildReadHoldingRegister(REG_WAVELENGTH, 1),

                "zero" => BuildWriteSingleRegister(0x0010, 0x0001), // 写入归零指令

                _ => throw new NotSupportedException($"不支持的命令: {command}")
            };
        }

        public string Decode(byte[] response)
        {
            if (response == null || response.Length < 5)
                throw new InvalidOperationException("Modbus 响应帧太短");

            if (response[0] != DEVICE_ADDR)
                throw new InvalidOperationException($"设备地址不匹配: 0x{response[0]:X2}");

            // 检查异常响应
            if ((response[1] & 0x80) != 0)
                throw new InvalidOperationException($"Modbus 异常码: 0x{response[2]:X2}");

            var byteCount = response[2];
            var data = response.Skip(3).Take(byteCount).ToArray();

            if (data.Length >= 4)
            {
                // 大端序：高字节在前
                var value = (data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3];
                return ((float)value / 1000.0).ToString("F4", CultureInfo.InvariantCulture);
            }

            if (data.Length >= 2)
            {
                var value = (data[0] << 8) | data[1];
                return (value / 10.0).ToString("F1", CultureInfo.InvariantCulture);
            }

            return BitConverter.ToString(data);
        }

        private static byte[] BuildReadHoldingRegister(ushort startAddr, ushort quantity)
        {
            var frame = new byte[8];
            frame[0] = DEVICE_ADDR;
            frame[1] = FUNC_READ_HOLDING;
            frame[2] = (byte)(startAddr >> 8);
            frame[3] = (byte)(startAddr & 0xFF);
            frame[4] = (byte)(quantity >> 8);
            frame[5] = (byte)(quantity & 0xFF);
            AppendCrc(frame, 6);
            return frame;
        }

        private static byte[] BuildWriteSingleRegister(ushort regAddr, ushort value)
        {
            var frame = new byte[8];
            frame[0] = DEVICE_ADDR;
            frame[1] = FUNC_WRITE_SINGLE;
            frame[2] = (byte)(regAddr >> 8);
            frame[3] = (byte)(regAddr & 0xFF);
            frame[4] = (byte)(value >> 8);
            frame[5] = (byte)(value & 0xFF);
            AppendCrc(frame, 6);
            return frame;
        }

        private static void AppendCrc(byte[] frame, int dataLength)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < dataLength; i++)
            {
                crc ^= frame[i];
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }
            frame[dataLength] = (byte)(crc & 0xFF);        // CRC 低字节
            frame[dataLength + 1] = (byte)(crc >> 8);       // CRC 高字节
        }
    }
}