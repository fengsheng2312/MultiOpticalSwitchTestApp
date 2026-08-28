using Free_spaceOpticalCirculatorTestApp.FileOperation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

/// <summary>
/// Pass/Fail 判定逻辑的边界值测试
/// 调用方式：PassFailTest.RunAll();  或  PassFailTest.RunAllAndWriteExcel();
/// </summary>
public static class PassFailTest
{
    /// <summary>
    /// 运行全部边界值测试，输出到控制台/调试窗口
    /// </summary>
    public static void RunAll()
    {
        var cases = GenerateBoundaryCases();
        int passCount = 0;
        int failCount = 0;
        var sb = new StringBuilder();

        sb.AppendLine("========== Pass/Fail 边界值测试 ==========");
        sb.AppendLine($"{"测试项",-16} {"测试值",8} {"预期",6} {"实际",6} {"结果",6}");
        sb.AppendLine(new string('-', 50));

        foreach (var c in cases)
        {
            bool actual = TestResultExcelWriter.JudgePassFail(c.TestItemName, c.TestValue);
            bool ok = actual == c.ExpectedPass;
            if (ok) passCount++; else failCount++;

            sb.AppendLine(
                $"{c.TestItemName,-16} " +
                $"{c.TestValue,8:F3} " +
                $"{(c.ExpectedPass ? "PASS" : "FAIL"),6} " +
                $"{(actual ? "PASS" : "FAIL"),6} " +
                $"{(ok ? "✓" : "✗ 失败"),6}");
        }

        sb.AppendLine(new string('-', 50));
        sb.AppendLine($"总计: {cases.Count} 条 | 通过: {passCount} | 失败: {failCount}");
        sb.AppendLine("==========================================");

        string report = sb.ToString();
        Console.WriteLine(report);
        Debug.WriteLine(report);

        if (failCount > 0)
        {
            System.Windows.Forms.MessageBox.Show(
                report,
                $"测试失败 ({failCount}/{cases.Count})",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Warning);
        }
        else
        {
            System.Windows.Forms.MessageBox.Show(
                report,
                "全部通过",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Information);
        }
    }

    /// <summary>
    /// 运行测试并把测试数据写入 Excel（验证填表+判定联动）
    /// </summary>
    public static async Task RunAllAndWriteExcel()
    {
        var cases = GenerateBoundaryCases();
        Console.WriteLine($"开始写入 {cases.Count} 条测试数据到 Excel...");

        foreach (var c in cases)
        {
            var row = new TestResultRow
            {
                DutSn = $"TEST-{c.TestItemName.GetHashCode():X8}",
                DutType = "边界值测试",
                TestItemName = c.TestItemName,
                ZeroValue = 0,
                ZeroSupplement = 0,
                PowerMeterReading = c.TestValue,
                TestResult = c.TestValue,
                ZeroDate = DateTime.Now
            };

            // 调用你原来的写入方法
            await TestResultExcelWriter.AppendResult(row, "自动化测试");
        }

        Console.WriteLine("测试数据写入完成，请打开 Excel 核对 Pass/Fail 列。");
        RunAll(); // 再跑一遍断言
    }

    // ============================================================
    //  边界值测试用例生成
    //  对每个阈值生成 3 个点：低于边界 / 等于边界 / 高于边界
    // ============================================================

    private class TestCase
    {
        public string TestItemName { get; set; }
        public double TestValue { get; set; }
        public bool ExpectedPass { get; set; }
        public string Remark { get; set; }
    }

    private static List<TestCase> GenerateBoundaryCases()
    {
        var cases = new List<TestCase>();

        // 每个测试项的规则：(名称, 阈值, 比较方向)
        var rules = new (string Name, double Threshold, bool IsGreaterOrEqual)[]
        {
            ("Com-Tx(ISO)",  21,  true),   // ≥ 21
            ("Tx-Com(IL)",   0.3, false),  // ≤ 0.3
            ("Com-Rx(IL)",   0.5, false),  // ≤ 0.5
            ("Rx-Com(ISO)",  21,  true),   // ≥ 21
            ("Tx-Tx(RL)",    35,  true),   // ≥ 35
            ("Com-Com(RL)",  35,  true),   // ≥ 35
            ("Tx-Rx(DIR)",   35,  true),   // ≥ 35
        };

        // 边界偏移量：IL 用 0.01，ISO/RL/DIR 用 0.1
        double smallOffset = 0.01;
        double largeOffset = 0.1;

        foreach (var (name, threshold, isGE) in rules)
        {
            double offset = (threshold < 1) ? smallOffset : largeOffset;

            if (isGE)
            {
                // ≥ 型：低于阈值→FAIL，等于阈值→PASS，高于阈值→PASS
                cases.Add(new TestCase
                {
                    TestItemName = name,
                    TestValue = threshold - offset,
                    ExpectedPass = false,
                    Remark = "低于边界"
                });
                cases.Add(new TestCase
                {
                    TestItemName = name,
                    TestValue = threshold,
                    ExpectedPass = true,
                    Remark = "等于边界"
                });
                cases.Add(new TestCase
                {
                    TestItemName = name,
                    TestValue = threshold + offset,
                    ExpectedPass = true,
                    Remark = "高于边界"
                });
            }
            else
            {
                // ≤ 型：低于阈值→PASS，等于阈值→PASS，高于阈值→FAIL
                cases.Add(new TestCase
                {
                    TestItemName = name,
                    TestValue = threshold - offset,
                    ExpectedPass = true,
                    Remark = "低于边界"
                });
                cases.Add(new TestCase
                {
                    TestItemName = name,
                    TestValue = threshold,
                    ExpectedPass = true,
                    Remark = "等于边界"
                });
                cases.Add(new TestCase
                {
                    TestItemName = name,
                    TestValue = threshold + offset,
                    ExpectedPass = false,
                    Remark = "高于边界"
                });
            }
        }

        // 额外补充：极端值 + 未配置测试项
        cases.Add(new TestCase { TestItemName = "Com-Tx(ISO)", TestValue = 999, ExpectedPass = true, Remark = "极端大值" });
        cases.Add(new TestCase { TestItemName = "Tx-Com(IL)", TestValue = -5, ExpectedPass = true, Remark = "负值(IL≤0.3)" });
        cases.Add(new TestCase { TestItemName = "Tx-Com(IL)", TestValue = 0, ExpectedPass = true, Remark = "零值" });
        cases.Add(new TestCase { TestItemName = "Unknown-Item", TestValue = 10, ExpectedPass = false, Remark = "未配置测试项" });
        cases.Add(new TestCase { TestItemName = "", TestValue = 10, ExpectedPass = false, Remark = "空测试项" });

        return cases;
    }
}
