using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace Free_spaceOpticalCirculatorTestApp.FileOperation
{
    /// <summary>
    /// 单行测试结果数据模型，字段与 Excel 列一一对应
    /// </summary>
    public class TestResultRow
    {
        /// <summary>环形器SN</summary>
        public string DutSn { get; set; }

        public string DutType { get; set; }
        /// <summary>序号-测试项（如 "1-Tx-Com(IL)"）</summary>
        public string TestItemName { get; set; }

        /// <summary>归零值(dBm)</summary>
        public double ZeroValue { get; set; }

        /// <summary>归零补充值</summary>
        public double ZeroSupplement { get; set; }

        /// <summary>功率计回读值(dBm)</summary>
        public double PowerMeterReading { get; set; }

        /// <summary>测试结果(差值)，即 回读值 - 归零值</summary>
        public double TestResult { get; set; }

        /// <summary>归零日期</summary>
        public DateTime ZeroDate { get; set; }
    }

    public static class TestResultExcelWriter
    {
        // 固定输出目录
        private static readonly string OutputDirectory = @"D:\TestResult\环形器测试";

        // 表头定义（只写一次）
        private static readonly string[] Headers =
        {
            "序号", "环形器SN", "环形器类型", "序号-测试项", "归零值", "归零补充值",
            "功率计回读值", "测试结果(差值)", "归零日期",
            "测试日期时间（YYYY-MM-DD HH:MM:SS）", "测试结论"
        };

        /// <summary>
        /// 追加一条测试结果到当日 Excel（每次调用只写一行）
        /// 文件路径固定为 D:\TestResult\环形器测试\dutTest-YYYY-MM-DD.xlsx
        /// 当日文件已存在则追加到末尾行，不存在则自动创建
        /// </summary>
        /// <param name="row">本次测试项的数据</param>
        /// <param name="operatorName">操作员</param>
        /// <param name="specMax">规格上限(dB)，超过则判定FAIL</param>
        public static void AppendResult(
    TestResultRow row,
    string operatorName,
    double specMax = 1.5)
        {
            try
            {
                ExcelPackage.License.SetNonCommercialPersonal("ZCC");

                // 空值保护
                if (row == null)
                    throw new ArgumentNullException(nameof(row), "TestResultRow 不能为空");

                // 自动构造当日文件路径
                string filePath = GetTodayFilePath();

                // 确保目录存在
                Directory.CreateDirectory(OutputDirectory);

                bool fileExists = File.Exists(filePath);

                using var package = fileExists
     ? new ExcelPackage(new FileInfo(filePath))
     : new ExcelPackage(new FileInfo(filePath));  // ← 新文件也要传入路径

                // ===== Sheet1: 测试结果 =====
                var sheet = fileExists
                    ? package.Workbook.Worksheets["测试结果"]
                    : package.Workbook.Worksheets.Add("测试结果");

                int currentRow;

                if (!fileExists)
                {
                    // 首次创建：写表头
                    WriteHeaders(sheet);
                    currentRow = 2;
                }
                else
                {
                    // 追加到最后一行的下一行
                    currentRow = sheet.Dimension?.End.Row + 1 ?? 2;
                }

                // ----- 填充当前行的各列 -----
                int seq = currentRow - 1; // 序号 = 行号 - 1（去掉表头）

                sheet.Cells[currentRow, 1].Value = seq;                                                        // 序号
                sheet.Cells[currentRow, 2].Value = row.DutSn ?? "";                                            // 环形器SN
                sheet.Cells[currentRow, 3].Value = row.DutType ?? "";                                          // 环形器类型
                sheet.Cells[currentRow, 4].Value = row.TestItemName ?? "";                                     // 序号-测试项
                sheet.Cells[currentRow, 5].Value = Math.Round(row.ZeroValue, 4);                               // 归零值
                sheet.Cells[currentRow, 6].Value = Math.Round(row.ZeroSupplement, 4);                          // 归零补充值
                sheet.Cells[currentRow, 7].Value = Math.Round(row.PowerMeterReading, 4);                       // 功率计回读值
                sheet.Cells[currentRow, 8].Value = Math.Round(row.TestResult, 3);                              // 测试结果(差值)
                sheet.Cells[currentRow, 9].Value = row.ZeroDate.ToString("yyyy-MM-dd");                                                                                               // ★ 关键修复：ZeroDate 可能为 null
                sheet.Cells[currentRow, 10].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");              // 测试日期时间

                // 测试结论：差值超规格标红 + FAIL
                bool pass = Math.Abs(row.TestResult) <= specMax;
                sheet.Cells[currentRow, 11].Value = pass ? "PASS" : "FAIL";
                sheet.Cells[currentRow, 11].Style.Font.Color.SetColor(pass ? Color.Green : Color.Red);
                sheet.Cells[currentRow, 11].Style.Font.Bold = true;

                // 超规格时，测试结果列也标红
                if (!pass)
                {
                    sheet.Cells[currentRow, 8].Style.Font.Color.SetColor(Color.Red);
                    sheet.Cells[currentRow, 8].Style.Font.Bold = true;
                }

                // 自适应列宽
                if (sheet.Dimension != null)
                {
                    sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
                }

                package.Save();
                Console.WriteLine($"测试结果已写入 Excel 文件: {filePath}");
            }
            catch (Exception ex)
            {
                // 记录日志或弹窗提示，避免程序直接崩溃
                MessageBox.Show(
                    $"错误信息：{ex.Message}\n\n堆栈：{ex.StackTrace}",
                    "Excel写入失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            
            throw; // 如果需要上层处理则保留 throw；如果只需要静默记录则删掉这行
            }
        }

        /// <summary>
        /// 获取当日 Excel 文件的完整路径
        /// 格式：D:\TestResult\环形器测试\dutTest-2026-08-21.xlsx
        /// </summary>
        private static string GetTodayFilePath()
        {
            string fileName = $"dutTest-{DateTime.Now:yyyy-MM-dd}.xlsx";
            return Path.Combine(OutputDirectory, fileName);
        }

        /// <summary>
        /// 写入 Sheet1 表头（带样式）
        /// </summary>
        private static void WriteHeaders(ExcelWorksheet sheet)
        {
            for (int i = 0; i < Headers.Length; i++)
            {
                var cell = sheet.Cells[1, i + 1];
                cell.Value = Headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.LightSteelBlue);
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }
        }
    }
}