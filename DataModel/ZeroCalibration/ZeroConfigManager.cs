using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Free_spaceOpticalCirculatorTestApp.DataModel.ZeroCalibration
{
    public static class ZeroConfigManager
    {
        private static readonly string ConfigPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "zero_calibration.json");

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        /// <summary>加载全部归零配置（多台盒子）</summary>
        public static ZeroConfig Load()
        {
            Console.WriteLine("进入到这里了");
            if (!File.Exists(ConfigPath))
            {
                Console.WriteLine($"归零值配置文件不存在，路径: {ConfigPath}");
                return new ZeroConfig();
            }
            Console.WriteLine("进入到这里了2");
            string json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<ZeroConfig>(json) ?? new ZeroConfig();
        }

        /// <summary>保存完整配置</summary>
        public static void Save(ZeroConfig config)
        {
            config.LastUpdated = DateTime.Now;
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            string json = JsonSerializer.Serialize(config, Options);

            // 【调试日志】打印真实的绝对路径
            string fullPath = Path.GetFullPath(ConfigPath);
            Console.WriteLine($"[ZeroConfigManager] 准备写入文件: {fullPath}");

            File.WriteAllText(fullPath, json);
            Console.WriteLine($"[ZeroConfigManager] 写入成功！文件大小: {new FileInfo(fullPath).Length} bytes");
        }

        /// <summary>获取或者创建指定SN的TestBox条目</summary>
        public static TestBoxZeroEntry GetOrCreateBoxEntry(ZeroConfig config, string testBoxSn)
        {
            var box = config.TestBoxes.FirstOrDefault(b => b.TestBoxSn == testBoxSn);
            if (box == null)
            {
                box = new TestBoxZeroEntry
                {
                    TestBoxSn = testBoxSn,
                    LastUpdated = DateTime.Now
                };
                config.TestBoxes.Add(box);
            }
            return box;
        }
        public static TestBoxZeroEntry GetOrCreateBoxEntry(String testBoxSn)
        {
            var config = Load();
            var box = config.TestBoxes.FirstOrDefault(b => b.TestBoxSn == testBoxSn);
            if (box == null)
            {
                box = new TestBoxZeroEntry
                {
                    TestBoxSn = testBoxSn,
                    LastUpdated = DateTime.Now
                };
                config.TestBoxes.Add(box);
            }
            return box;
        }

        /// <summary>更新单条归零记录
        /// </summary>
        /// <param name="testBoxSn">归属的测试盒序列号</param>
        /// <param name="item">归零记录，内部不再带DeviceSn</param>
        public static void UpdateItem(string testBoxSn, ZeroCalibration item)
        {
            var config = Load();

            var boxEntry = GetOrCreateBoxEntry(config, testBoxSn);

            // 移除同TestItemId旧记录
            var exist = boxEntry.Items.FirstOrDefault(x => x.TestItemId == item.TestItemId);
            if (exist != null)
                boxEntry.Items.Remove(exist);

            boxEntry.Items.Add(item);
            boxEntry.LastUpdated = DateTime.Now;
            config.LastUpdated = DateTime.Now;
            Save(config);
        }


        /// <summary>根据【测试项ID + 设备SN】读取归零值，没有返回null</summary>
        public static double? GetZeroValue(String testItemName, string testBoxSn)
        {
            var config = Load();
            var box = config.TestBoxes.FirstOrDefault(b => b.TestBoxSn == testBoxSn);
            if (box == null)
                return null;

            var item = box.Items.FirstOrDefault(x => x.TestItemName == testItemName);
            return item?.ZeroValueDbm;
        }

        /// <summary>删除某一台测试盒全部归零数据</summary>
        public static void RemoveTestBox(string testBoxSn)
        {
            var config = Load();
            var box = config.TestBoxes.FirstOrDefault(b => b.TestBoxSn == testBoxSn);
            if (box != null)
            {
                config.TestBoxes.Remove(box);
                Save(config);
            }
        }
    }
}
