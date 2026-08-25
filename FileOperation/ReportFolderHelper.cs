using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace Free_spaceOpticalCirculatorTestApp.FileOperation
{

    /// <summary>
    /// 测试报告文件夹处理工具类
    /// 优先路径：D:\TestResult\环形器测试
    /// 降级路径：LocalApplicationData\TestResult\环形器测试
    /// </summary>
    public static class ReportFolderHelper
    {
        /// <summary>
        /// 获取环形器测试报告目录，优先D盘；D盘异常自动降级到系统本地目录
        /// </summary>
        /// <returns>一定返回可用路径，不会返回null</returns>
        public static string GetCirculatorTestReportFolder()
        {
            // 主路径 D盘
            string primaryDir = @"D:\TestResult\环形器测试";
            string resultDir = CheckAndCreateDirectory(primaryDir);

            // D盘创建失败，走降级路径
            if (string.IsNullOrEmpty(resultDir))
            {
                string localBase = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string fallbackDir = Path.Combine(localBase, @"TestResult\环形器测试");
                resultDir = CheckAndCreateDirectory(fallbackDir);
            }

            // 理论不会走到null，兜底返回程序运行目录
            if (string.IsNullOrEmpty(resultDir))
            {
                resultDir = Path.Combine(Application.StartupPath, @"TestResult\环形器测试");
                Directory.CreateDirectory(resultDir);
            }

            return resultDir;
        }

        /// <summary>
        /// 通用：检查目录，不存在自动创建，异常返回null
        /// </summary>
        /// <param name="directoryPath">目标绝对路径</param>
        /// <returns>成功返回路径；失败返回null</returns>
        private static string CheckAndCreateDirectory(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
                return directoryPath;
            }
            catch (DriveNotFoundException)
            {
                // 驱动器不存在，无D盘
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                // 权限不足
                return null;
            }
            catch (IOException)
            {
                // 磁盘满、路径非法
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

}
