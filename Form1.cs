using Microsoft.VisualBasic.Logging;
using MultiOpticalSwitchTestApp.DataModel;
using MultiOpticalSwitchTestApp.DataModel.ZeroCalibration;
using MultiOpticalSwitchTestApp.Device;
using MultiOpticalSwitchTestApp.FileOperation;
using MultiOpticalSwitchTestApp.SoftwareUpdate;
using MultiOpticalSwitchTestApp.Command;
using OpticalMeter.Contract;
using OpticalMeter.Host;
using Plugins.GenericScpiPowerMeter;
using Sunny.UI;
using Sunny.UI.Win32;
using System.Collections.Generic; 
using System.IO.Ports;
using System.Management;
using System.Reflection;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static System.ComponentModel.Design.ObjectSelectorEditor;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace MultiOpticalSwitchTestApp
{
    public partial class Form1 : UIForm
    {
        private bool _isUpdateChecking;
        private const string curVersion = "V1.0.9";
        private const string modifytime = "2026-09-03";

        private AutoUpdateService? _autoUpdate;
        private readonly PluginManager _pluginManager = new();
        private IPowerMeter? _currentDevice;
        private bool _isRefreshingComPort = false;
        private readonly Dictionary<UIComboBox, bool> _comboOpenStatus = new();
        private UIComboBox _currentOpenCombo = null;
        private readonly SwitchDevice _switchDevice;
        private readonly SwitchDevice _switchDevice2;
        // 光开关切通道指令生成器
        private readonly command _switchCommand = new();
        // 通道按钮只生成一次，避免重复加载
        private bool _switchChannelButtonsBuilt;
        // 开关板原始接收缓存，用来拼接分片字节

        //烧录初始化

        // 全量轮询相关：取消源 + 结果窗体
        private CancellationTokenSource? _pollCts;
        private SwitchPollForm? _pollForm;

        public Form1()
        {
            InitializeComponent();
            LoadFormIcon();
            this.Text = $"Core调节测试软件";
            _switchDevice = new SwitchDevice();
            _switchDevice2 = new SwitchDevice();
            BindCombo(uiComboBox1);
            BindCombo(switchCom);
            checkAll.Click += CheckAll_Click;
            this.Load += Form1_Load;
        }
        private void LoadFormIcon()
        {
            // 资源全名格式：RootNamespace.文件夹.文件名（RootNamespace 是 BurnWorker3，不是 BurnWorker）
            string resName = "MultiOpticalSwitchTestApp.Assets.logo.ico";
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream(resName);
            if (stream == null)
            {
                // 把程序集里真实存在的嵌入资源名列出来，便于对照大小写与命名空间
                string names = string.Join("\n", asm.GetManifestResourceNames());
                MessageBox.Show($"资源未找到，名称：{resName}\n当前程序集嵌入资源：\n{names}");
                return;
            }
            try
            {
                // 复制一份再赋值：Icon 需要自己持有数据，直接依赖会被 using 释放的流不可靠
                using var tmp = new Icon(stream);
                this.Icon = (Icon)tmp.Clone();
            }
            catch (Exception ex)
            {
                // 图标文件损坏或格式非法时只记录，绝不能让主窗体启动失败
                Console.WriteLine($"图标加载失败：{ex.Message}");
            }
        }

        private async Task DoBindFixAsync()
        {
            await BindFixTextBoxFocusAsync(this);
        }

        /// <summary>
        /// 分段递归绑定，大量控件不阻塞UI
        /// </summary>
        private async Task BindFixTextBoxFocusAsync(Control parent)
        {
            int count = 0;
            foreach (Control ctl in parent.Controls)
            {
                if (ctl is TextBoxBase tb)
                {
                    tb.MouseDown -= TextBoxFix_MouseDown;
                    tb.MouseDown += TextBoxFix_MouseDown;
                }

                if (ctl.HasChildren)
                {
                    await BindFixTextBoxFocusAsync(ctl);
                }

                count++;
                if (count >= 20)
                {
                    count = 0;
                    await Task.Delay(1); // 让出UI消息循环
                }
            }
        }

        private void TextBoxFix_MouseDown(object sender, MouseEventArgs e)
        {
            var tb = sender as TextBoxBase;
            if (tb == null) return;

            // 这里鼠标点击时控件一定有句柄，BeginInvoke安全
            tb.BeginInvoke(new Action(() =>
            {
                tb.Focus();
                tb.Select();
            }));
        }
        private void Form_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            foreach (var kv in _comboOpenStatus)
            {
                var cbx = kv.Key;
                if (_comboOpenStatus[cbx])
                {
                    cbx.HideDropDown();
                }
            }
        }
        private void SwitchDevice_OnDataReceived(string data)
        {
            if (IsDisposed) return;
            Invoke(() => { });
        }
        private void SwitchDevice_OnDisconnected()
        {
            if (this.IsDisposed) return;
            Invoke(new Action(() =>
            {
                Console.WriteLine("【上层】串口已异常断开，UI重置");
                uiButton2.Text = "打开";
            }));
        }
        private void SwitchDevice_OnError(Exception ex)
        {
            if (this.IsDisposed) return;
            Invoke(new Action(() =>
            {
                MessageBox.Show($"串口异常：{ex.Message}");
            }));
        }
        private async void Form1_Load(object sender, EventArgs e)
        {
            // 界面加载完成、控件已经创建完毕，在这里刷新串口下拉框
            _ = RefreshAllSerialPortCombosAsync();
            await LoadPlugins();
            var cbos = new UIComboBox[] { SwitchBaudrate, baudrate };
            uiComboBox2.SelectedIndex = 0;
            foreach (var cbo in cbos)
            {
                if (cbo.Items.Count > 0)
                {
                    cbo.SelectedIndex = 0;
                }
            }
            // 订阅设备业务事件（IO后台线程，UI必须Invoke）
            // 订阅设备业务事件（IO后台线程，UI必须Invoke）
            _switchDevice.OnDataReceived += SwitchDevice_OnDataReceived;
            _switchDevice.OnDisconnected += SwitchDevice_OnDisconnected;
            _switchDevice.OnError += SwitchDevice_OnError;
            //====新增订阅完整帧事件====
            _switchDevice.OnDeviceFrameComplete += SwitchDevice_OnDeviceFrameComplete;

            _switchDevice2.OnDataReceived += SwitchDevice_OnDataReceived;
            _switchDevice2.OnDisconnected += SwitchDevice_OnDisconnected;
            _switchDevice2.OnError += SwitchDevice_OnError;
            _switchDevice2.OnDeviceFrameComplete += SwitchDevice2_OnDeviceFrameComplete;

            SerialGlobalManager.SerialLogEvent += OnSerialLog;
            if (SwitchBaudrate.Items.Count > 0)
            {
                SwitchBaudrate.SelectedIndex = 0;  // 代码可以写，属性面板看不见
            }
            // 
            uiSymbolLabel1.Text = $"版本：{Form1.AppVersion}\n时间：{Form1.BuildTime}";
            Console.WriteLine($"版本：{Form1.AppVersion}\n\n修改时间：{Form1.BuildTime}");
            BuildSwitchChannelButtons();
            _ = DoBindFixAsync();
            // 启动后自动检查更新（静默模式：无更新/失败不打扰用户，仅写日志；发现新版本才弹窗）
            //_ = CheckAndPromptUpdateAsync(silentWhenNoUpdate: true, silentOnError: true);
        }
        private void SwitchDevice_OnDeviceFrameComplete(byte[] frameBytes)
        {
            try
            {
                string gbkText = Encoding.GetEncoding("GBK").GetString(frameBytes);
                Console.WriteLine($"【开关板GBK完整应答】{gbkText}");
                AppendGbkLog(gbkText);
            }
            catch
            {
                //二进制数据GBK解码乱码直接忽略
                Console.WriteLine($"帧GBK解码失败，原始字节：{BitConverter.ToString(frameBytes)}");
            }
        }
        private void SwitchDevice2_OnDeviceFrameComplete(byte[] frameBytes)
        {
            try
            {
                string gbkText = Encoding.GetEncoding("GBK").GetString(frameBytes);
                Console.WriteLine($"【开关板GBK完整应答】{gbkText}");
                AppendGbkLog(gbkText);
            }
            catch
            {
                //二进制数据GBK解码乱码直接忽略
                Console.WriteLine($"帧GBK解码失败，原始字节：{BitConverter.ToString(frameBytes)}");
            }
        }

        /// <summary>把GBK中文输出到rtb_Log，自动跨线程</summary>
        private void AppendGbkLog(string text)
        {
            if (rtb_Log.InvokeRequired)
            {
                rtb_Log.Invoke(new Action<string>(AppendGbkLog), text);
                return;
            }
            string line = $"{DateTime.Now:HH:mm:ss} [RX] {text}\r\n";
            const int MaxLines = 2000;
            if (rtb_Log.Lines.Length > MaxLines)
            {
                rtb_Log.Lines = rtb_Log.Lines.Skip(rtb_Log.Lines.Length - MaxLines).ToArray();
            }
            rtb_Log.AppendText(line);
            rtb_Log.SelectionStart = rtb_Log.TextLength;
            rtb_Log.ScrollToCaret();
        }

        /// <summary>
        /// 读取 Config/SwitchCh.json 中 1310 波长的 switches 列表，
        /// 每个开关对应一个 uiTableLayoutPanel，按 channels 生成通道按钮。
        /// </summary>
        private void BuildSwitchChannelButtons()
        {
            if (_switchChannelButtonsBuilt) return;

            var panels = new UITableLayoutPanel[]
            {
                uiTableLayoutPanel1, uiTableLayoutPanel2, uiTableLayoutPanel3,
                uiTableLayoutPanel4, uiTableLayoutPanel5
            };

            string configPath = Path.Combine(AppContext.BaseDirectory, "Config", "SwitchCh.json");
            if (!File.Exists(configPath))
            {
                Console.WriteLine($"[SwitchCh] 配置文件不存在: {configPath}");
                return;
            }

            var switches = new List<(int Id, string Name, List<int> Channels, List<string> Cores)>();
            try
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(configPath));
                if (!doc.RootElement.TryGetProperty("wavelengths", out JsonElement wavelengths) ||
                    !wavelengths.TryGetProperty("1310", out JsonElement wl1310) ||
                    !wl1310.TryGetProperty("switches", out JsonElement switchesArr))
                {
                    Console.WriteLine("[SwitchCh] 未找到 wavelengths.1310.switches 节点");
                    return;
                }

                foreach (JsonElement sw in switchesArr.EnumerateArray())
                {
                    int id = sw.GetProperty("id").GetInt32();
                    string name = sw.GetProperty("name").GetString() ?? $"Switch{id}";
                    var channels = new List<int>();
                    foreach (JsonElement ch in sw.GetProperty("channels").EnumerateArray())
                    {
                        channels.Add(ch.GetInt32());
                    }
                    // core 与 channels 一一对应，缺失时补空字符串
                    var cores = new List<string>();
                    if (sw.TryGetProperty("core", out JsonElement coreArr) && coreArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement cv in coreArr.EnumerateArray())
                        {
                            cores.Add(cv.ValueKind == JsonValueKind.String ? cv.GetString() ?? "" : cv.ToString());
                        }
                    }
                    while (cores.Count < channels.Count) cores.Add("");
                    switches.Add((id, name, channels, cores));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取 SwitchCh.json 失败：{ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            int panelCount = Math.Min(panels.Length, switches.Count);
            if (switches.Count != panels.Length)
            {
                Console.WriteLine($"[SwitchCh] 开关数({switches.Count})与面板数({panels.Length})不一致，仅生成前 {panelCount} 组");
            }

            for (int p = 0; p < panelCount; p++)
            {
                UITableLayoutPanel panel = panels[p];
                (int id, string name, List<int> channels, List<string> cores) = switches[p];
                panel.Controls.Clear();

                // 列：Core编号标签 | 输入框 | 通道按钮 | 剩余填充
                panel.ColumnCount = 4;
                panel.ColumnStyles.Clear();
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74F));
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64F));
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

                // 第0行开关名称，之后每个通道占一行
                panel.RowCount = channels.Count + 1;
                panel.RowStyles.Clear();
                panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
                for (int r = 1; r < panel.RowCount; r++)
                {
                    panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / channels.Count));
                }

                var nameLabel = new UILabel
                {
                    Text = name,
                    Dock = DockStyle.Fill,
                    Margin = new Padding(6, 2, 6, 0),
                    TextAlign = ContentAlignment.MiddleLeft,
                    BackColor = Color.Transparent,
                    ForeColor = Color.FromArgb(48, 48, 48),
                    Font = new Font("宋体", 11F, FontStyle.Bold),
                };
                panel.Controls.Add(nameLabel);
                panel.SetCellPosition(nameLabel, new TableLayoutPanelCellPosition(0, 0));
                panel.SetColumnSpan(nameLabel, 4);

                for (int c = 0; c < channels.Count; c++)
                {
                    int channel = channels[c];
                    int row = c + 1;

                    var coreLabel = new UILabel
                    {
                        Text = "Core编号:",
                        Dock = DockStyle.Fill,
                        Margin = new Padding(6, 0, 0, 0),
                        TextAlign = ContentAlignment.MiddleLeft,
                        BackColor = Color.Transparent,
                        ForeColor = Color.FromArgb(48, 48, 48),
                        Font = new Font("宋体", 9F)

                    };
                    panel.Controls.Add(coreLabel);
                    panel.SetCellPosition(coreLabel, new TableLayoutPanelCellPosition(0, row));

                    var coreTextBox = new UITextBox
                    {
                        Name = $"coreTextBox_{id}_{channel}",
                        Dock = DockStyle.Fill,
                        Margin = new Padding(2, 7, 4, 7),
                        Text = c < cores.Count ? cores[c] : string.Empty,
                    };
                    panel.Controls.Add(coreTextBox);
                    panel.SetCellPosition(coreTextBox, new TableLayoutPanelCellPosition(1, row));

                    var btn = new UIButton
                    {
                        Text = $"通道:{channel}",
                        Dock = DockStyle.Fill,
                        Margin = new Padding(4, 6, 8, 6),
                        Font = new Font("宋体", 10F),
                    };
                    btn.Tag = (id, channel, name, panel);
                    SetSwitchButtonSelected(btn, false);
                    btn.Click += SwitchChannelButton_Click;

                    panel.Controls.Add(btn);
                    panel.SetCellPosition(btn, new TableLayoutPanelCellPosition(2, row));
                }
            }

            _switchChannelButtonsBuilt = true;
            Console.WriteLine($"[SwitchCh] 已生成 {panelCount} 组通道按钮");
        }

        /// <summary>通道按钮点击：同组内互斥高亮，并向开关板下发切通道指令。</summary>
        private async void SwitchChannelButton_Click(object sender, EventArgs e)
        {
            if (sender is not UIButton btn) return;
            if (btn.Tag is not (int id, int channel, string switchName, UITableLayoutPanel panel)) return;

            // 点击即保存：若左侧 Core 输入框有值，同步刷新 SwitchCh.json 与 zeroDiff.json
            SaveCoreIfFilled(panel, id, channel, switchName);

            if (!_switchDevice.IsOpened)
            {
                MessageBox.Show("开关串口未打开，请先打开串口后再切换通道！", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                byte[] frame = _switchCommand.SwitchLightChannel(id, channel);
                // 收发内容已由 SerialGlobalManager 的日志事件统一输出到 rtb_Log
                await _switchDevice.WriteBytesAsync(frame);

                // 指令发送成功后：同一个 uiTableLayoutPanel 下当前按钮变黑，其余恢复白色
                foreach (Control ctrl in panel.Controls)
                {
                    if (ctrl is UIButton other)
                    {
                        SetSwitchButtonSelected(other, ReferenceEquals(other, btn));
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"切换通道失败：{ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 读取按钮同行的 Core 输入框，非空时把值写回
        /// Config/SwitchCh.json 的 switches[].core[channels下标]，
        /// 并把 Config/zeroDiff.json 中同 switch_name+channel 的条目 core 全部刷新。
        /// </summary>
        private static void SaveCoreIfFilled(UITableLayoutPanel panel, int id, int channel, string switchName)
        {
            var box = panel.Controls.Find($"coreTextBox_{id}_{channel}", false)
                           .OfType<UITextBox>().FirstOrDefault();
            string coreValue = box?.Text?.Trim() ?? string.Empty;
            if (coreValue.Length == 0) return;

            foreach (string configDir in GetConfigWriteDirs())
            {
                string switchChPath = Path.Combine(configDir, "SwitchCh.json");
                string zeroDiffPath = Path.Combine(configDir, "zeroDiff.json");
                try
                {
                    bool changed = false;
                    if (File.Exists(switchChPath))
                    {
                        var root = JsonNode.Parse(File.ReadAllText(switchChPath));
                        var switches = root?["wavelengths"]?["1310"]?["switches"]?.AsArray();
                        if (switches != null)
                        {
                            foreach (var node in switches)
                            {
                                if (node is null || NodeInt(node["id"]) != id) continue;
                                var chArr = node["channels"]?.AsArray();
                                int idx = -1;
                                for (int i = 0; i < (chArr?.Count ?? 0); i++)
                                {
                                    if (NodeInt(chArr[i]) == channel) { idx = i; break; }
                                }
                                if (idx < 0) break;

                                var coreArr = node["core"]?.AsArray();
                                if (coreArr == null)
                                {
                                    coreArr = new JsonArray();
                                    node["core"] = coreArr;
                                }
                                while (coreArr.Count <= idx) coreArr.Add((string?)null);
                                if (CoreText(coreArr[idx]) != coreValue)
                                {
                                    coreArr[idx] = coreValue;
                                    changed = true;
                                }
                                break;
                            }
                        }
                        if (changed)
                        {
                            root!["updated_at"] = DateTime.Now.ToString("yyyy-MM-dd");
                            File.WriteAllText(switchChPath, root.ToJsonString(JsonConfigWriteOptions));
                            Console.WriteLine($"[SwitchCh] 已更新 {switchChPath}：Switch{id} 通道{channel} core={coreValue}");
                        }
                    }

                    if (File.Exists(zeroDiffPath))
                    {
                        var zroot = JsonNode.Parse(File.ReadAllText(zeroDiffPath));
                        var zeroDiff = zroot?["wavelengths"]?["1310"]?["zero_diff"]?.AsObject();
                        int updated = 0;
                        if (zeroDiff != null)
                        {
                            foreach (var kv in zeroDiff)
                            {
                                var entry = kv.Value;
                                if (entry is null) continue;
                                if (CoreText(entry["switch_name"]) == switchName &&
                                    NodeInt(entry["channel"]) == channel &&
                                    CoreText(entry["core"]) != coreValue)
                                {
                                    entry["core"] = coreValue;
                                    updated++;
                                }
                            }
                        }
                        if (updated > 0)
                        {
                            zroot!["updated_at"] = DateTime.Now.ToString("yyyy-MM-dd");
                            File.WriteAllText(zeroDiffPath, zroot.ToJsonString(JsonConfigWriteOptions));
                            Console.WriteLine($"[zeroDiff] 已更新 {zeroDiffPath}：{switchName} 通道{channel} 共{updated}条 core={coreValue}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"写入配置文件失败（{configDir}）：{ex.Message}", "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>中文不转义、缩进输出的 JSON 写回选项。</summary>
        private static readonly JsonSerializerOptions JsonConfigWriteOptions = new()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        /// <summary>
        /// 配置写回目录：运行时 BaseDirectory\Config，
        /// 以及源码工程目录\Config（能定位到时，防止下次构建用源码副本覆盖运行时改动）。
        /// </summary>
        private static IEnumerable<string> GetConfigWriteDirs()
        {
            yield return Path.Combine(AppContext.BaseDirectory, "Config");

            // 迭代器不能在 try(含catch) 中 yield，先定位再返回
            string? srcConfig = null;
            try
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null)
                {
                    var csproj = dir.GetFiles("*.csproj").FirstOrDefault();
                    if (csproj != null)
                    {
                        string src = Path.Combine(dir.FullName, "Config");
                        if (Directory.Exists(src) &&
                            !string.Equals(src, Path.Combine(AppContext.BaseDirectory, "Config"),
                                StringComparison.OrdinalIgnoreCase))
                        {
                            srcConfig = src;
                        }
                        break;
                    }
                    dir = dir.Parent;
                }
            }
            catch
            {
                // 定位源码目录失败时只写运行时目录
            }

            if (srcConfig != null) yield return srcConfig;
        }

        /// <summary>取 JSON 节点文本（兼容 null/数字），不可读时返回空串。</summary>
        private static string CoreText(JsonNode? node)
        {
            if (node is null) return string.Empty;
            return node.GetValueKind() == JsonValueKind.Null ? string.Empty : node.ToString();
        }

        /// <summary>取 JSON 节点整数值（兼容数字/字符串数字），无法解析时返回 -1。</summary>
        private static int NodeInt(JsonNode? node)
        {
            if (node is null) return -1;
            try
            {
                var kind = node.GetValueKind();
                if (kind is JsonValueKind.Number or JsonValueKind.String)
                {
                    string s = node.ToString();
                    return int.TryParse(s, out int p) ? p : -1;
                }
            }
            catch
            {
                // 类型不符时走默认返回
            }
            return -1;
        }

        /// <summary>
        /// 设置通道按钮外观。
        /// 选中=黑底白字；未选中=白底黑字，悬停/按下时按钮底色变灰、字体颜色保持不变。
        /// StyleCustomMode 必须开启，否则 SunnyUI 会按默认 Style 在悬停时把文字重绘成白色。
        /// </summary>
        private static void SetSwitchButtonSelected(UIButton btn, bool selected)
        {
            btn.StyleCustomMode = true;
            if (selected)
            {
                btn.FillColor = Color.Black;
                btn.FillHoverColor = Color.FromArgb(60, 60, 60);
                btn.FillPressColor = Color.FromArgb(40, 40, 40);
                btn.RectColor = Color.FromArgb(48, 48, 48);
                btn.RectHoverColor = Color.FromArgb(48, 48, 48);
                btn.RectPressColor = Color.FromArgb(48, 48, 48);
                btn.ForeColor = Color.White;
                btn.ForeHoverColor = Color.White;
                btn.ForePressColor = Color.White;
            }
            else
            {
                btn.FillColor = Color.White;
                btn.FillHoverColor = Color.FromArgb(224, 224, 224);
                btn.FillPressColor = Color.FromArgb(200, 200, 200);
                btn.RectColor = Color.FromArgb(220, 220, 220);
                btn.RectHoverColor = Color.FromArgb(190, 190, 190);
                btn.RectPressColor = Color.FromArgb(190, 190, 190);
                btn.ForeColor = Color.FromArgb(48, 48, 48);
                btn.ForeHoverColor = Color.FromArgb(48, 48, 48);
                btn.ForePressColor = Color.FromArgb(48, 48, 48);
            }
        }

        #region 全量轮询测试（checkAll 开表，表内“开始/停止轮询”驱动）

        // 当前轮询会话：波长、表格数据、开关名→id、光源序号→json 里生效的归零值
        private string _pollWaveLen = "1310";
        private List<SwitchPollForm.PollItem> _pollItems = new();
        private readonly Dictionary<string, int> _pollNameToId = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, double> _pollJsonZeroValues = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 解析轮询序号输入（如 1~15/1/1,2,3/1，2，3）为序号集合。
        /// 支持范围写法（a~b / a-b，顺序可颠倒）与单个数字，
        /// 分隔符 / \ , ， ; ； 空格 换行 均可；未填写或格式非法返回 null 由调用方提示。
        /// </summary>
        private static HashSet<int>? ParseCheckListNumbers(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            const int MaxSpan = 512;
            var result = new HashSet<int>();
            string[] tokens = input.Split(
                new[] { '/', '\\', ',', '，', ';', '；', ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);

            foreach (string raw in tokens)
            {
                string tk = raw.Trim();
                if (tk.Length == 0) continue;

                int sep = FirstRangeSeparator(tk);
                if (sep > 0)
                {
                    if (!int.TryParse(tk.Substring(0, sep), out int lo) ||
                        !int.TryParse(tk.Substring(sep + 1), out int hi))
                        return null;
                    if (lo <= 0 || hi <= 0) return null;
                    if (lo > hi) (lo, hi) = (hi, lo);
                    if (hi - lo + 1 > MaxSpan) return null;
                    for (int v = lo; v <= hi; v++) result.Add(v);
                }
                else
                {
                    if (!int.TryParse(tk, out int n) || n <= 0) return null;
                    result.Add(n);
                }
            }

            return result.Count == 0 ? null : result;
        }

        /// <summary>返回序号范围分隔符（~ ～ - — –）最早出现的位置，不存在返回 -1。</summary>
        private static int FirstRangeSeparator(string text)
        {
            // 半角波浪号/全角波浪号/连字符/破折号/短破折号
            const string seps = "\u007E\uFF5E\u002D\u2014\u2013";
            for (int i = 0; i < text.Length; i++)
            {
                if (seps.IndexOf(text[i]) >= 0) return i;
            }
            return -1;
        }

        /// <summary>
        /// checkAll：按 waveLenText 波长读取 Config/zeroDiff.json 的 zero_diff，
        /// 只弹出数据表窗体；开关切换与读数由表内“开始轮询”按钮触发，打开时不自动执行。
        /// </summary>
        private void CheckAll_Click(object sender, EventArgs e)
        {
            string waveLen = waveLenText.Text.Trim();
            if (string.IsNullOrEmpty(waveLen))
            {
                MessageBox.Show("请先在波长输入框中填写波长（如 1310），再点击全量轮询。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 轮询序号过滤：依据 checkList.Text 生成序号集合，未命中的 zero_diff 条目不生成表格行
            HashSet<int>? wantNos = ParseCheckListNumbers(checkList.Text);
            if (wantNos == null)
            {
                MessageBox.Show("请先填写轮询序号，支持：单个数字(1,2,3)、范围(1~15)、混合(1~8/10,12)，分隔符 / 或 , 均可。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string configDir = Path.Combine(AppContext.BaseDirectory, "Config");
            string zeroDiffPath = Path.Combine(configDir, "zeroDiff.json");
            string switchChPath = Path.Combine(configDir, "SwitchCh.json");
            if (!File.Exists(zeroDiffPath) || !File.Exists(switchChPath))
            {
                MessageBox.Show($"缺少配置文件：{zeroDiffPath} 或 {switchChPath}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var items = new List<SwitchPollForm.PollItem>();
            var nameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var jsonZero = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            try
            {
                // 开关名 → id 映射（切换指令需要 id）
                var swRoot = JsonNode.Parse(File.ReadAllText(switchChPath));
                var swArr = swRoot?["wavelengths"]?[waveLen]?["switches"]?.AsArray();
                if (swArr == null)
                {
                    MessageBox.Show($"SwitchCh.json 中未找到 wavelengths.{waveLen}.switches 节点", "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                foreach (var node in swArr)
                {
                    if (node is null) continue;
                    nameToId[CoreText(node["name"])] = NodeInt(node["id"]);
                }

                // zero_diff 逐条读取，按光源序号排序
                var zRoot = JsonNode.Parse(File.ReadAllText(zeroDiffPath));
                if (zRoot?["wavelengths"]?[waveLen]?["zero_diff"] is not JsonObject zeroDiffObj)
                {
                    MessageBox.Show($"zeroDiff.json 中未找到 wavelengths.{waveLen}.zero_diff 节点", "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                var ordered = new List<KeyValuePair<string, JsonNode?>>(zeroDiffObj);
                ordered.Sort((a, b) =>
                {
                    int na = int.TryParse(a.Key, out int x) ? x : int.MaxValue;
                    int nb = int.TryParse(b.Key, out int y) ? y : int.MaxValue;
                    return na.CompareTo(nb);
                });
                foreach (var kv in ordered)
                {
                    var entry = kv.Value;
                    if (entry is null) continue;

                    // 序号不在 checkList 生成的序列里 → 该条目不进表格
                    if (wantNos != null && (!int.TryParse(kv.Key, out int no) || !wantNos.Contains(no)))
                        continue;

                    double zero = NodeDouble(entry["zero_value"]);
                    items.Add(new SwitchPollForm.PollItem(
                        kv.Key,
                        zero,
                        CoreText(entry["core"]),
                        CoreText(entry["switch_name"]),
                        NodeInt(entry["channel"])));
                    jsonZero[kv.Key] = zero;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取配置失败：{ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (items.Count == 0)
            {
                MessageBox.Show($"按轮询序号「{checkList.Text.Trim()}」过滤后，wavelengths.{waveLen}.zero_diff 没有可显示的数据。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 已有表格时先停掉上一轮并关闭，保证窗口与会话数据一一对应
            if (_pollForm is { IsDisposed: false })
            {
                StopPolling();
                _pollForm.Close();
            }

            _pollWaveLen = waveLen;
            _pollItems = items;
            _pollNameToId.Clear();
            foreach (var kv in nameToId) _pollNameToId[kv.Key] = kv.Value;
            _pollJsonZeroValues.Clear();
            foreach (var kv in jsonZero) _pollJsonZeroValues[kv.Key] = kv.Value;

            var form = new SwitchPollForm(waveLen, items);
            form.StartRequested += PollStartRequested;
            form.StopRequested += (_, _) => StopPolling();
            form.FormClosed += (_, _) => StopPolling();
            _pollForm = form;
            form.Show(this);
        }

        /// <summary>表内“开始轮询”：校验串口与功率计后启动后台线程，打开时不自动执行。</summary>
        private void PollStartRequested(object? sender, EventArgs e)
        {
            if (_pollCts != null) return;

            var form = _pollForm;
            if (!_switchDevice.IsOpened)
            {
                MessageBox.Show("开关串口未打开，请先打开串口后再启动轮询！", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                form?.SetRunning(false);
                return;
            }

            var device = _currentDevice;
            if (device == null || !device.IsConnected)
            {
                MessageBox.Show("请先连接功率计再启动轮询！", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                form?.SetRunning(false);
                return;
            }

            if (_pollItems.Count == 0)
            {
                MessageBox.Show("没有可轮询的数据，请重新点击“全量轮询”。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                form?.SetRunning(false);
                return;
            }

            var cts = new CancellationTokenSource();
            _pollCts = cts;
            form?.SetRunning(true);
            _ = Task.Run(() => RunPollLoopAsync(cts.Token));
        }

        /// <summary>主界面 stopAll：取消后台轮询线程，表格保留最后一次的数值。</summary>
        private void StopAll_Click(object sender, EventArgs e)
        {
            if (_pollCts == null)
            {
                MessageBox.Show("当前没有正在运行的轮询。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            StopPolling();
        }

        /// <summary>停止并清理轮询取消源（幂等），同时复位表内按钮状态。</summary>
        private void StopPolling()
        {
            var cts = _pollCts;
            _pollCts = null;
            if (cts == null) return;
            cts.Cancel();
            cts.Dispose();
            _pollForm?.SetRunning(false);
        }

        /// <summary>
        /// 后台轮询：逐条切开关→读功率→刷新表格，一轮完成再下一轮；
        /// 每轮把表格里的归零值与 zeroDiff.json 比对，不一致则写回配置文件。
        /// </summary>
        private async Task RunPollLoopAsync(CancellationToken token)
        {
            var form = _pollForm;
            var items = _pollItems;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    foreach (var it in items)
                    {
                        token.ThrowIfCancellationRequested();

                        if (!_pollNameToId.TryGetValue(it.SwitchName, out int switchId))
                            throw new InvalidOperationException($"SwitchCh.json 中未找到开关 {it.SwitchName}");

                        var device = _currentDevice;
                        if (device == null || !device.IsConnected)
                            throw new InvalidOperationException("功率计已断开，轮询停止");

                        if (!_switchDevice.IsOpened)
                            throw new InvalidOperationException("开关串口已离线，轮询停止");

                        // 归零值以表格当前内容为准（用户可手工修改）
                        double zeroValue = form?.GetZeroValue(it.LightNo) ?? it.ZeroValue;

                        // 与通道按钮同一套指令与串口链路
                        byte[] frame = _switchCommand.SwitchLightChannel(switchId, it.Channel);
                        await _switchDevice.WriteBytesAsync(frame);
                        await Task.Delay(120, token); // 等待光路切换稳定

                        double power = await device.ReadPowerAsync();
                        double actual = power + zeroValue;
                        form?.UpdateResult(it.LightNo, power.ToString("F4"), actual.ToString("F4"));

                        // 检测到与 json 中的归零值不一致时写回
                        if (ZeroDiffers(zeroValue, JsonZeroOf(it.LightNo)))
                        {
                            SaveZeroValueToConfig(_pollWaveLen, it.LightNo, zeroValue);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常停止
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        StopPolling();
                        MessageBox.Show($"轮询已停止：{msg}", "错误",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
                catch
                {
                    // 窗体已关闭，忽略
                }
            }
            finally
            {
                form?.SetRunning(false);
            }
        }

        /// <summary>当前会话中某光源在 zeroDiff.json 里的归零值基线。</summary>
        private double JsonZeroOf(string lightNo)
        {
            return _pollJsonZeroValues.TryGetValue(lightNo, out double v) ? v : 0.0;
        }

        /// <summary>
        /// 归零值差异判定：表格按 4 位小数显示，因此两边都按 4 位小数比较，
        /// 避免因显示精度造成“没改却写回”的误判。
        /// </summary>
        private static bool ZeroDiffers(double a, double b)
        {
            return Math.Abs(Math.Round(a, 4) - Math.Round(b, 4)) >= 0.00005;
        }

        /// <summary>把表格中的归零值写回 Config/zeroDiff.json（运行时目录与源码目录同步）。</summary>
        private void SaveZeroValueToConfig(string waveLen, string lightNo, double value)
        {
            foreach (string configDir in GetConfigWriteDirs())
            {
                string zeroDiffPath = Path.Combine(configDir, "zeroDiff.json");
                if (!File.Exists(zeroDiffPath)) continue;
                try
                {
                    var root = JsonNode.Parse(File.ReadAllText(zeroDiffPath));
                    if (root?["wavelengths"]?[waveLen]?["zero_diff"] is not JsonObject zeroDiffObj) continue;
                    if (zeroDiffObj[lightNo] is not JsonObject entry) continue;

                    double current = NodeDouble(entry["zero_value"]);
                    if (!ZeroDiffers(value, current)) continue;

                    entry["zero_value"] = Math.Round(value, 4);
                    root["updated_at"] = DateTime.Now.ToString("yyyy-MM-dd");
                    File.WriteAllText(zeroDiffPath, root.ToJsonString(JsonConfigWriteOptions));
                    Console.WriteLine($"[zeroDiff] 已更新 {zeroDiffPath}：光源{lightNo} zero_value={Math.Round(value, 4)}");
                }
                catch (Exception ex)
                {
                    string msg = ex.Message;
                    string dir = configDir;
                    try
                    {
                        BeginInvoke(new Action(() =>
                            MessageBox.Show($"写入 zeroDiff.json 失败（{dir}）：{msg}", "错误",
                                MessageBoxButtons.OK, MessageBoxIcon.Error)));
                    }
                    catch
                    {
                        // 窗体已关闭，忽略
                    }
                }
            }

            // 基线更新，避免下一轮重复写同一个值
            _pollJsonZeroValues[lightNo] = Math.Round(value, 4);
        }

        /// <summary>取 JSON 节点小数值（兼容数字/字符串），无法解析时返回 0。</summary>
        private static double NodeDouble(JsonNode? node)
        {
            if (node is null) return 0.0;
            try
            {
                var kind = node.GetValueKind();
                if (kind == JsonValueKind.Number) return node.GetValue<double>();
                if (kind == JsonValueKind.String)
                    return double.TryParse(node.ToString(), out double d) ? d : 0.0;
            }
            catch
            {
                // 类型不符时走默认返回
            }
            return 0.0;
        }

        #endregion

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 关闭窗体前停止后台轮询
            StopPolling();
            // 解绑本窗体UI事件
            _switchDevice.OnDataReceived -= SwitchDevice_OnDataReceived;
            _switchDevice.OnDisconnected -= SwitchDevice_OnDisconnected;
            SerialGlobalManager.SerialLogEvent -= OnSerialLog;
            _switchDevice.OnDeviceFrameComplete -= SwitchDevice_OnDeviceFrameComplete;
            _switchDevice.OnError -= SwitchDevice_OnError;
            // 2.释放全部串口硬件资源
            SerialGlobalManager.DisposeAllPorts();
        }
        /// <summary>
        /// 刷新串口列表，已被全局管理器打开的端口标记
        /// </summary>
        /// <summary>
        /// 【异步后台扫描串口，不阻塞UI】刷新三个下拉框
        /// </summary>
        // 原来：private async void RefreshAllSerialPortCombosAsync()
        // 修改为：
        //private async Task RefreshAllSerialPortCombosAsync()
        //{
        //    if (_isRefreshingComPort) return;
        //    _isRefreshingComPort = true;
        //    try
        //    {
        //        var portNames = await Task.Run(() =>
        //        {
        //            return SerialPort.GetPortNames().OrderBy(p => p).ToArray();
        //        });

        //        string sel1 = uiComboBox1.Text;
        //        string sel2 = uiComboBox3.Text;
        //        string sel3 = uiComboBox5.Text;

        //        uiComboBox1.Items.Clear();
        //        uiComboBox3.Items.Clear();
        //        uiComboBox5.Items.Clear();

        //        foreach (var com in portNames)
        //        {
        //            bool isOnline = SerialGlobalManager.IsPortOnline(com);
        //            string displayText = isOnline ? $"{com} [已连接]" : com;

        //            uiComboBox1.Items.Add(displayText);
        //            uiComboBox3.Items.Add(displayText);
        //            uiComboBox5.Items.Add(displayText);
        //        }

        //        uiComboBox1.Text = sel1;
        //        uiComboBox3.Text = sel2;
        //        uiComboBox5.Text = sel3;
        //    }
        //    finally
        //    {
        //        _isRefreshingComPort = false;
        //    }
        //}
        private async Task RefreshAllSerialPortCombosAsync()
        {
            if (_isRefreshingComPort) return;
            _isRefreshingComPort = true;

            try
            {
                // 1. 获取系统真正可用的COM集合（SerialPort只返回可打开的COM）
                HashSet<string> realAvailableComs;
                try
                {
                    realAvailableComs = new HashSet<string>(SerialPort.GetPortNames());
                }
                catch
                {
                    realAvailableComs = new HashSet<string>();
                }

                var portInfoList = await Task.Run(() =>
                {
                    var tempList = new List<(string ComPort, string DeviceName)>();

                    try
                    {
                        // PNP查询所有Ports设备：CH340、CP2102、FT232、蓝牙COM、原生串口全部在这里
                        using var searcher = new ManagementObjectSearcher(
                            @"SELECT Name FROM Win32_PnPEntity WHERE PNPClass='Ports'");

                        foreach (var mo in searcher.Get())
                        {
                            string fullName = mo["Name"]?.ToString() ?? string.Empty;
                            if (string.IsNullOrWhiteSpace(fullName)) continue;

                            // 匹配末尾 (COMxx)
                            var match = System.Text.RegularExpressions.Regex.Match(fullName, @"\((COM\d+)\)$");
                            if (!match.Success) continue;

                            string com = match.Groups[1].Value;
                            string devName = fullName.Replace($"({com})", "").Trim();

                            tempList.Add((com, devName));
                        }
                    }
                    catch
                    {
                        // WMI查询失败，只保留COM号，设备名为空
                    }

                    // 兜底：把 GetPortNames 里面存在、但是WMI没读到的COM补进来
                    foreach (var comName in realAvailableComs)
                    {
                        bool exist = tempList.Any(x => x.ComPort == comName);
                        if (!exist)
                        {
                            tempList.Add((comName, string.Empty));
                        }
                    }

                    // 只保留系统真正可用的COM，过滤掉PNP里无效/禁用的COM
                    tempList = tempList.Where(x => realAvailableComs.Contains(x.ComPort)).ToList();

                    // COM号去重
                    tempList = tempList.GroupBy(x => x.ComPort).Select(g => g.First()).ToList();

                    // COM数字升序排序
                    return tempList.OrderBy(x =>
                    {
                        if (int.TryParse(x.ComPort.Replace("COM", ""), out int num))
                            return num;
                        return int.MaxValue;
                    }).ToList();
                });

                // ========= UI主线程操作 =========
                // 保存【原始COM端口号】，不再保存显示文本，修复刷新后选不中BUG
                string? lastSel1 = (uiComboBox1.SelectedItem as ComPortItem)?.ComPort;
                string? lastSel2 = (switchCom.SelectedItem as ComPortItem)?.ComPort;

                uiComboBox1.Items.Clear();
                switchCom.Items.Clear();

                foreach (var info in portInfoList)
                {
                    bool isOnline = SerialGlobalManager.IsPortOnline(info.ComPort);
                    var item = new ComPortItem
                    {
                        ComPort = info.ComPort,
                        DeviceName = info.DeviceName,
                        IsOnline = isOnline
                    };
                    uiComboBox1.Items.Add(item);
                    switchCom.Items.Add(item);
                }

                // 恢复上次选中：按ComPort匹配，不受显示文字、[已连接]标签影响
                foreach (var cbo in new[] { uiComboBox1, switchCom })
                {
                    string? targetCom = cbo == uiComboBox1 ? lastSel1 : lastSel2;
                    if (!string.IsNullOrEmpty(targetCom))
                    {
                        var found = cbo.Items.OfType<ComPortItem>().FirstOrDefault(p => p.ComPort == targetCom);
                        if (found != null)
                        {
                            cbo.SelectedItem = found;
                        }
                    }

                    // 无选中并且有选项，默认选第一个
                    if (cbo.SelectedIndex == -1 && cbo.Items.Count > 0)
                    {
                        cbo.SelectedIndex = 0;
                    }
                }
            }
            finally
            {
                _isRefreshingComPort = false;
            }
        }
        private void BindCombo(UIComboBox cbx)
        {
            _comboOpenStatus[cbx] = false;
            cbx.MouseDown += Combo_MouseDown;
            cbx.DropDown += (s, e) => _comboOpenStatus[cbx] = true;
            cbx.DropDownClosed += (s, e) => _comboOpenStatus[cbx] = false;
        }

        private void Combo_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (!(sender is UIComboBox cbx)) return;

            if (_comboOpenStatus[cbx])
            {
                // 延迟关闭
                Task.Run(async () =>
                {
                    await Task.Delay(30);
                    cbx.Invoke(new Action(() =>
                    {
                        cbx.HideDropDown();
                    }));
                });
            }
        }
        //private void UpdateMarkLabel(string text)
        //{
        //    if (uiMarkLabel1.InvokeRequired)
        //    {
        //        uiMarkLabel1.BeginInvoke(new Action(() => UpdateMarkLabel(text)));
        //    }
        //    else
        //    {
        //        uiMarkLabel1.Text = text;
        //    }
        //}
        public static string ExtractComPort(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            // 匹配 COM + 一位或多位数字，兼容 COM1 ~ COM999
            Match match = Regex.Match(input, @"COM\d+", RegexOptions.IgnoreCase);
            return match.Success ? match.Value.ToUpper() : null;
        }
        private async void uiButton1_Click_1(object sender, EventArgs e)
        {
            ITransport? transport = null;
            IPowerMeter? device = null;

            try
            {
                var model = Ommodel.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(model))
                {
                    MessageBox.Show("请选择设备型号", "提示");
                    return;
                }
                var protocolName = Omprotocol.SelectedItem?.ToString();
                var transportName = Omtransport.SelectedItem?.ToString();
                var plugin = _pluginManager.FindPlugin(model, protocolName, transportName);
                if (plugin == null)
                {
                    MessageBox.Show($"未找到支持 {model} 的插件", "错误");
                    return;
                }
                Console.WriteLine("有找到插件");
                connectButton.Enabled = false;
                transport = CreateTransport();
                Console.WriteLine($"[DEBUG] 创建传输成功: {transport.GetType().Name}");

                // ✅ 真正调用插件提供的协议工厂，MainForm 不 new 任何协议
                var protocolOptions = plugin.GetProtocolOptions()?.ToList();
                var protocolFactory = protocolOptions?
                    .FirstOrDefault(p => string.Equals(p.Item1, protocolName, StringComparison.OrdinalIgnoreCase))
                    .Item2;

                if (protocolFactory == null)
                {
                    MessageBox.Show($"插件未提供协议 [{protocolName}] 的工厂实现", "错误");
                    connectButton.Enabled = true;
                    transport.Dispose();
                    return;
                }

                var protocol = protocolFactory.Invoke();
                Console.WriteLine($"[DEBUG] 使用插件工厂创建协议: {protocol.GetType().Name}");
                Console.WriteLine($"[DEBUG] 找到插件: {plugin?.GetType().Name} 协议: {protocolName} 传输: {transportName}");
                //_currentDevice = plugin.Create(transport, protocol, new Dictionary<string, string>());
                String transportParams = ExtractComPort(switchCom.Text) + ":" + baudrate.Text;
                await transport.OpenAsync(transportParams);

                device = plugin.Create(transport, protocol, new Dictionary<string, string>
                {
                    ["model"] = model
                });
                _currentDevice = device;
                Global.opticalPowermeter = device;
                Console.WriteLine($"[DEBUG] 创建设备实例: {device.GetType().FullName}+{protocol.GetType().Name}");
                Console.WriteLine($"[DEBUG] 打开成功: {device.GetType().FullName}+{protocol.GetType().Name}");
            }
            catch (Exception ex)
            {
                if (device != null && ReferenceEquals(_currentDevice, device))
                {
                    _currentDevice = null;
                    Global.opticalPowermeter = null;
                    try
                    {
                        await device.DisconnectAsync();
                    }
                    catch (Exception cleanupEx)
                    {
                        Console.WriteLine($"[DEBUG] 连接失败后的清理异常: {cleanupEx.Message}");
                    }
                    device.Dispose();
                    connectButton.Enabled = true;
                    MessageBox.Show($"连接失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else if (device == null)
                {
                    transport?.Dispose();
                    connectButton.Enabled = true;
                    MessageBox.Show($"连接失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

        }

        private async void uiButton5_Click(object sender, EventArgs e)
        {
            var device = _currentDevice;
            if (device == null || !device.IsConnected)
            {
                MessageBox.Show("请先连接功率计", "提示");
                return;
            }
            try
            {
                var power = await device.ReadPowerAsync();
                if (ReferenceEquals(_currentDevice, device))
                {
                    uiTextBox1.Text = power.ToString("F4");
                }
            }
            catch (OperationCanceledException) when (!ReferenceEquals(_currentDevice, device))
            {
                // Disconnect canceled the pending read.
            }
            catch (Exception ex)
            {
                if (ReferenceEquals(_currentDevice, device))
                    MessageBox.Show($"功率读取失败: {ex.Message}", "错误");
            }
            finally
            {
                uiButton5.Enabled = true;
            }
        }

        private void uiGroupBox1_Click_1(object sender, EventArgs e)
        {

        }

        private void uiGroupBox2_Click(object sender, EventArgs e)
        {

        }
        //加载插件
        private async Task LoadPlugins()
        {
            var pluginsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");

            // ★ 调试：确认路径和目录内容
            Console.WriteLine($"[DEBUG] 插件目录: {pluginsDir}");
            Console.WriteLine($"[DEBUG] 目录存在: {System.IO.Directory.Exists(pluginsDir)}");

            if (System.IO.Directory.Exists(pluginsDir))
            {
                var files = System.IO.Directory.GetFiles(pluginsDir, "*.dll");
                Console.WriteLine($"[DEBUG] DLL文件数: {files.Length}");
                foreach (var f in files)
                {
                    Console.WriteLine($"[DEBUG]   - {System.IO.Path.GetFileName(f)}");
                }
            }
            var powerMeterPlugin = new GenericScpiPowerMeterPlugin();
            _pluginManager.ScanAndLoad(pluginsDir);
            _pluginManager.RegisterPlugin(powerMeterPlugin);
            var allModels = new List<string>();
            foreach (var plugin in _pluginManager.Plugins)
            {
                allModels.AddRange(plugin.SupportedModels);
            }
            Ommodel.Items.AddRange(allModels.ToArray());
            // ✅ 本地直接使用这个对象
            foreach (var model in powerMeterPlugin.SupportedModels)
            {
                Console.WriteLine($"Supported Model: {model}");
            }


            //// 填充插件列表 —— ★ 新增：显示支持的传输方式
            //// ★ 填充前移除事件，避免逐个 Add 时反复触发 SelectedIndexChanged
            //_cmbModel.SelectedIndexChanged -= CmbModel_SelectedIndexChanged;

            //var allModels = new List<string>();
            //foreach (var plugin in _pluginManager.Plugins)
            //{
            //    allModels.AddRange(plugin.SupportedModels);
            //}
            //_cmbModel.Items.AddRange(allModels.ToArray());

            //// ★ 填充完后再绑回事件
            //_cmbModel.SelectedIndexChanged += CmbModel_SelectedIndexChanged;

            //if (_cmbModel.Items.Count > 0) _cmbModel.SelectedIndex = 0;

            //_lblStatus.Text = $"就绪 | 已加载 {_pluginManager.Plugins.Count} 个插件";
        }

        //private void Ommodel_SelectedIndexChanged(object sender, EventArgs e)
        //{
        //    var model = Ommodel.SelectedItem?.ToString();

        //    if (Ommodel.SelectedIndex < 0 || Ommodel.Items.Count == 0 || string.IsNullOrWhiteSpace(model))
        //    {
        //        Omtransport.Items.Clear();
        //        Omprotocol.Items.Clear();
        //        return;
        //    }

        //    var plugin = _pluginManager.FindPlugin(model);
        //    if (plugin == null)
        //    {
        //        Omtransport.Items.Clear();
        //        Omprotocol.Items.Clear();
        //        Console.WriteLine($"[WARN] 找不到型号插件: {model}");
        //        return;
        //    }

        //    // ==========关键改动：强转拿到插件内部配置字典==========
        //    if (plugin is not Plugins.GenericScpiPowerMeter.GenericScpiPowerMeterPlugin scpiPlugin)
        //    {
        //        Omtransport.Items.Clear();
        //        Omprotocol.Items.Clear();
        //        Console.WriteLine($"[WARN] {model} 不是SCPI功率计插件");
        //        return;
        //    }

        //    if (!scpiPlugin.DeviceConfigs.TryGetValue(model, out PowerMeterConfig? cfg))
        //    {
        //        Omtransport.Items.Clear();
        //        Omprotocol.Items.Clear();
        //        Console.WriteLine($"[WARN] 在插件DeviceConfigs找不到型号配置:{model}");
        //        return;
        //    }

        //    Omtransport.Items.Clear();
        //    Omprotocol.Items.Clear();

        //    // =========填充协议下拉=========
        //    if (plugin.SupportedProtocols != null && plugin.SupportedProtocols.Count > 0)
        //    {
        //        foreach (var protocol in plugin.SupportedProtocols)
        //        {
        //            Omprotocol.Items.Add(protocol);
        //            Console.WriteLine($"[DEBUG] 型号 '{model}' 支持协议: {protocol}");
        //        }
        //    }
        //    else
        //    {
        //        Console.WriteLine($"[WARN] 型号 '{model}' SupportedProtocols为空");
        //    }

        //    // =========填充传输下拉：优先JSON配置，没有则回退接口plugin.SupportedTransports=========
        //    List<string> transportItems = new List<string>();

        //    if (cfg.SupportedTransports != null && cfg.SupportedTransports.Count > 0)
        //    {
        //        foreach (var t in cfg.SupportedTransports)
        //        {   
        //            Console.WriteLine($"[DEBUG] {model} SupportedTransports from JSON: {t}");
        //            var s = t.ToString();
        //            transportItems.Add(s);
        //            Omtransport.Items.Add(s);
        //            Console.WriteLine($"[DEBUG][FromJson] {model} supported transport:{s}");
        //        }
        //    }
        //    else
        //    {
        //        foreach (var s in plugin.SupportedTransports)
        //        {
        //            transportItems.Add(s);
        //            Omtransport.Items.Add(s);
        //            Console.WriteLine($"[DEBUG][FromPluginInterface] {model} supported transport:{s}");
        //        }
        //    }

        //    // =========协议默认选中=========
        //    if (Omprotocol.Items.Count > 0)
        //    {
        //        Omprotocol.SelectedIndex = 0;
        //    }

        //    // =========传输：优先匹配cfg.DefaultTransport=========
        //    int selectedTransportIndex = -1;
        //    string defaultTransConfig = cfg.DefaultTransport ?? string.Empty;

        //    if (!string.IsNullOrWhiteSpace(defaultTransConfig) && Omtransport.Items.Count > 0)
        //    {
        //        for (int i = 0; i < Omtransport.Items.Count; i++)
        //        {
        //            string? itemText = Omtransport.Items[i]?.ToString();
        //            if (string.Equals(itemText, defaultTransConfig, StringComparison.OrdinalIgnoreCase))
        //            {
        //                selectedTransportIndex = i;
        //                break;
        //            }
        //        }
        //    }

        //    if (selectedTransportIndex >= 0)
        //    {
        //        Omtransport.SelectedIndex = selectedTransportIndex;
        //    }
        //    else if (Omtransport.Items.Count > 0)
        //    {
        //        Omtransport.SelectedIndex = 0;
        //        Console.WriteLine($"[WARN] 未匹配DefaultTransport[{defaultTransConfig}]，回退第一项");
        //    }
        //}

        private void Ommodel_SelectedIndexChanged(object sender, EventArgs e)
        {
            var model = Ommodel.SelectedItem?.ToString();

            if (Ommodel.SelectedIndex < 0 || Ommodel.Items.Count == 0 || string.IsNullOrWhiteSpace(model))
            {
                Omtransport.Items.Clear();
                Omprotocol.Items.Clear();
                return;
            }

            var plugin = _pluginManager.FindPlugin(model);
            if (plugin == null)
            {
                Omtransport.Items.Clear();
                Omprotocol.Items.Clear();
                Console.WriteLine($"[WARN] 找不到型号插件: {model}");
                return;
            }

            Console.WriteLine($"[INFO] 选中型号: {model}, 插件: {plugin.GetType().Name}");

            Omtransport.Items.Clear();
            Omprotocol.Items.Clear();

            // ========== 协议下拉：统一用 GetProtocolOptions ==========
            var protocolOptions = plugin.GetProtocolOptions()?.ToList();
            if (protocolOptions != null && protocolOptions.Count > 0)
            {
                foreach (var (name, _) in protocolOptions)
                {
                    Omprotocol.Items.Add(name);
                    Console.WriteLine($"[DEBUG] {model} 支持协议: {name}");
                }
            }
            else
            {
                Console.WriteLine($"[WARN] {model} GetProtocolOptions返回空");
            }

            // ========== 传输下拉：统一用 GetSupportedTransports(model) ==========
            var transports = plugin.GetSupportedTransports(model)?.ToList() ?? new List<string>();

            if (transports.Count > 0)
            {
                foreach (var t in transports)
                {
                    Omtransport.Items.Add(t);
                    Console.WriteLine($"[DEBUG] {model} 支持传输: {t}");
                }
            }
            else
            {
                Console.WriteLine($"[WARN] {model} GetSupportedTransports返回空");
            }

            // ========== 默认选中 ==========
            if (Omprotocol.Items.Count > 0) Omprotocol.SelectedIndex = 0;
            if (Omtransport.Items.Count > 0) Omtransport.SelectedIndex = 0;
        }


        /// <summary>
        /// 从Omtransport下拉框获取选中的传输枚举
        /// </summary>
        /// <param name="transportType">输出枚举</param>
        /// <returns>true成功，false失败</returns>
        private bool TryGetSelectedTransport(out TransportType transportType)
        {
            transportType = default;
            string? transStr = Omtransport.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(transStr))
            {
                Console.WriteLine("[ERROR] 未选择传输方式");
                return false;
            }

            if (!Enum.TryParse<TransportType>(transStr, ignoreCase: true, out transportType))
            {
                Console.WriteLine($"[ERROR] 无法解析传输方式字符串：{transStr}");
                return false;
            }
            return true;
        }


        private ITransport CreateTransport()
        {
            return Omtransport.SelectedItem?.ToString() switch
            {
                "TCP" => new OpticalMeter.Transport.TcpTransport(),
                "Serial" => new OpticalMeter.Transport.SerialTransport(),
                "USB-HID" => new OpticalMeter.Transport.UsbHidTransport(),
                _ => throw new NotSupportedException("不支持的传输方式")
            };
        }

        private IProtocol CreateProtocol()
        {
            String selectedProtocol = Omprotocol.SelectedItem?.ToString() ?? string.Empty;
            foreach (String key in Global.protocols.Keys)
            {
                Console.WriteLine($"[DEBUG] Global.protocols contains: {key}");
            }
            if (Global.protocols.ContainsKey(selectedProtocol))
            {
                Console.WriteLine($"[DEBUG] 使用插件提供的协议工厂创建协议实例: {Global.protocols[selectedProtocol]}");
                Console.WriteLine($"[DEBUG] 协议类型: {Global.protocols[selectedProtocol].Name}");
                return Global.protocols[selectedProtocol];
            }
            else
            {
                Console.WriteLine($"[DEBUG] 使用默认协议工厂创建协议实例: {selectedProtocol}");
                return Omprotocol.SelectedItem?.ToString() switch
                {
                    "SCPI" => new OpticalMeter.Protocol.ScpiProtocol(),
                    "VendorPrivate" => new OpticalMeter.Protocol.VendorPrivateProtocol(),
                    "Modbus" => new OpticalMeter.Protocol.ModbusProtocol(),
                    // ★ JW8102Private 协议由插件内部自行创建，此处返回 ScpiProtocol 作为占位
                    _ => throw new NotSupportedException("不支持的协议")
                };
            }

        }

        private async void uiCheckBoxGroup1_ValueChanged(object sender, CheckBoxGroupEventArgs e)
        {

        }

        private async void uiButton7_Click(object sender, EventArgs e)
        {
            // Remove the shared reference before awaiting I/O cancellation. This makes
            // a concurrent connect/read unable to publish the old device again.
            var device = _currentDevice;
            if (device == null)
                return;

            if (!ReferenceEquals(
                    Interlocked.CompareExchange(ref _currentDevice, null, device),
                    device))
            {
                return;
            }

            try
            {
                Console.WriteLine($"[DEBUG] 正在断开连接: {device.GetType().FullName}");
                await device.DisconnectAsync();
                Console.WriteLine($"[DEBUG] 设备已断开连接: {device.GetType().FullName}");
                device.Dispose();
                Console.WriteLine($"[DEBUG] 设备资源已释放: {device.GetType().FullName}");
                Global.opticalPowermeter = null;
                MessageBox.Show("设备已断开连接", "信息");
                connectButton.Enabled = true;
            }
            catch (Exception ex)
            {
                device.Dispose();
                Global.opticalPowermeter = null;
                connectButton.Enabled = true;
                MessageBox.Show($"断开失败: {ex.Message}", "错误");
            }
        }

        private void uiButton2_Click_1(object sender, EventArgs e)
        {
            try
            {
                var selectItem = uiComboBox1.SelectedItem as ComPortItem;
                if (selectItem == null)
                {
                    MessageBox.Show("请先选择串口！");
                    return;
                }

                // 👉 判断：当前是要【打开】才赋值串口、波特率；关闭时不修改配置
                if (uiButton2.Text == "打开")
                {
                    string comName = selectItem.ComPort.Trim();
                    int baudrate;
                    if (!int.TryParse(SwitchBaudrate.Text, out baudrate))
                    {
                        baudrate = 115200;
                    }

                    // 仅打开前设置参数
                    _switchDevice.ComPort = comName;
                    _switchDevice.BaudRate = baudrate;

                    _switchDevice.Open();
                    bool ok = SerialGlobalManager.IsPortOnline(_switchDevice.ComPort);
                    Console.WriteLine($"打开后端口状态:{ok}");
                    uiButton2.Text = "关闭";
                    uiLight1.OnColor = Color.Green;
                }
                else
                {
                    // 关闭：直接Close，不再修改ComPort/BaudRate
                    _switchDevice.Close();
                    uiButton2.Text = "打开";
                    uiLight1.OnColor = Color.Red;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设备操作失败：{ex.Message}");
            }
        }
        /// <summary>
        /// 打印日志到接收框
        /// </summary>
        /// <param name="msg">消息内容</param>
        /// <param name="isSend">true=发送消息，false=接收消息</param>
        private void OnSerialLog(bool isSend, string logText)
        {
            if (rtb_Log.IsDisposed) return;

            if (rtb_Log.InvokeRequired)
            {
                rtb_Log.Invoke(new Action(() => OnSerialLog(isSend, logText)));
                return;
            }

            string prefix = isSend ? "[TX] " : "[RX] ";
            string line = $"{DateTime.Now:HH:mm:ss} {prefix}{logText}\r\n";

            // 最多保留2000行，超出就截断
            const int MaxLines = 2000;
            if (rtb_Log.Lines.Length > MaxLines)
            {
                rtb_Log.Lines = rtb_Log.Lines.Skip(rtb_Log.Lines.Length - MaxLines).ToArray();
            }

            rtb_Log.AppendText(line);
            rtb_Log.SelectionStart = rtb_Log.TextLength;
            rtb_Log.ScrollToCaret();
        }


        private async void uiButton6_Click_1(object sender, EventArgs e)
        {
            await RefreshAllSerialPortCombosAsync();
        }
        public class ComPortItem
        {
            public string ComPort { get; set; } = string.Empty;
            public string DeviceName { get; set; } = string.Empty;
            public bool IsOnline { get; set; }

            // ComboBox显示文本
            public override string ToString()
            {
                string disp = string.IsNullOrWhiteSpace(DeviceName) ? ComPort : $"{ComPort} - {DeviceName}";
                if (IsOnline) disp += " [已连接]";
                return disp;
            }
        }
        public static byte[] HexStringToByteArray(string hexStr)
        {
            if (string.IsNullOrWhiteSpace(hexStr))
                throw new ArgumentException("输入不能为空");

            // 按空格分割，过滤空片段（多个空格容错）
            var parts = hexStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            byte[] buf = new byte[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                buf[i] = Convert.ToByte(parts[i], 16);
            }
            return buf;
        }
        private bool _isSending = false;
        private async void uiButton8_Click(object sender, EventArgs e)
        {
            if (_isSending) return;
            try
            {
                _isSending = true;

                if (!_switchDevice.IsOpened)
                {
                    MessageBox.Show("串口已经离线，请重新打开串口！");
                    return;
                }
                string inputText = uiTextBox7.Text.Trim();
                byte[] sendBytes = HexStringToByteArray(inputText);
                await _switchDevice.WriteBytesAsync(sendBytes);
            }
            catch (FormatException)
            {
                MessageBox.Show("十六进制格式错误！示例：AA 55 01 01");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发送失败：{ex.Message}");
            }
            finally
            {
                _isSending = false;
            }
        }

        private void rtb_Log_TextChanged(object sender, EventArgs e)
        {

        }

        private void uiComboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            Global.readChannel = uiComboBox2.SelectedItem?.ToString() ?? string.Empty;
        }

        private void uiButton14_Click(object sender, EventArgs e)
        {
            rtb_Log.Clear();
        }

        private void uiSymbolLabel1_Click(object sender, EventArgs e)
        {

        }
        /// <summary>当前软件版本号，自动从程序集读取（对应 .csproj 里的 <Version>）</summary>
        public static string AppVersion
        {
            get
            {
                try
                {
                    // 优先用 ProductVersion（对应 .csproj 的 <Version>，3 段式如 1.0.9）
                    var loc = Assembly.GetExecutingAssembly().Location;
                    if (!string.IsNullOrEmpty(loc) && File.Exists(loc))
                    {
                        var ver = System.Diagnostics.FileVersionInfo.GetVersionInfo(loc).ProductVersion;
                        // 去掉 Git 提交哈希后缀（如 1.0.0+abc1234 → 1.0.0）
                        if (!string.IsNullOrEmpty(ver))
                        {
                            int plusIdx = ver.IndexOf('+');
                            if (plusIdx > 0) ver = ver.Substring(0, plusIdx);
                            return ver;
                        }

                    }
                    // 单文件发布时回退到 AssemblyVersion（4 段式，去掉末尾的 .0）
                    var asmVer = Assembly.GetExecutingAssembly().GetName().Version;
                    if (asmVer is not null)
                        return asmVer.Revision == 0
                            ? $"{asmVer.Major}.{asmVer.Minor}.{asmVer.Build}"
                            : asmVer.ToString();
                }
                catch { }
                return "1.0.0";
            }
        }

        private async void uiSymbolLabel7_Click(object sender, EventArgs e)
        {
            try
            {
                String updateUrl = "//192.168.0.150/研发部文件共享/软件组/SoftwareRepository/环形器自动测试/update/appcast.xml";
                var appcast = updateUrl.Trim();
                if (string.IsNullOrWhiteSpace(appcast))
                {
                    Console.WriteLine("请先填写 AppCast 地址");
                    MessageBox.Show("请先填写 AppCast 地址（http(s):// 或 \\\\server\\share\\appcast.xml）",
                        "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                _autoUpdate = new AutoUpdateService(this, appcast, Log);
                _autoUpdate.CheckUpdate();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"检查更新失败: {ex.Message}");
                Console.WriteLine($"堆栈: {ex.StackTrace}");
                MessageBox.Show($"更新失败: {ex.Message}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void Log(string msg)
        {
            if (rtb_Log.InvokeRequired)
            {
                rtb_Log.Invoke(new Action<string>(Log), msg);
                return;
            }
            rtb_Log.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\r\n");
            rtb_Log.ScrollToCaret();
        }

        private void uiLabel8_Click(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// 检查更新并按需弹窗（直连 SMB 共享目录模式F
        /// silentWhenNoUpdate=true：无新版本时不弹"已是最新"（用于启动自动检查）
        /// silentOnError=true：检查失败时不弹错误框，仅写日志（用于启动自动检查）
        /// </summary>


        /// <summary>构建时间，从程序集元数据读取（.csproj 里配置，编译时自动注入）</summary>
        /// <summary>构建时间，直接读取 exe 文件的最后修改时间（不需要 .csproj 配置）</summary>
        public static string BuildTime
        {
            get
            {
                try
                {
                    var loc = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    if (!string.IsNullOrEmpty(loc) && System.IO.File.Exists(loc))
                        return System.IO.File.GetLastWriteTime(loc).ToString("yyyy-MM-dd HH:mm:ss");
                }
                catch { }
                return "未知";
            }
        }



    }
}
