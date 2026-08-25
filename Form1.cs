using Free_spaceOpticalCirculatorTestApp.DataModel;
using Free_spaceOpticalCirculatorTestApp.DataModel.ZeroCalibration;
using Free_spaceOpticalCirculatorTestApp.Device;
using Free_spaceOpticalCirculatorTestApp.FileOperation;
using OpticalMeter.Contract;
using OpticalMeter.Host;
using Plugins.GenericScpiPowerMeter;
using Sunny.UI;
using Sunny.UI.Win32;
using System.Collections.Generic; 
using System.IO.Ports;
using System.Management;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Text.Json;
using static System.ComponentModel.Design.ObjectSelectorEditor;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Free_spaceOpticalCirculatorTestApp
{
    public partial class Form1 : UIForm
    {
        private readonly PluginManager _pluginManager = new();
        private IPowerMeter? _currentDevice;
        private bool _isRefreshingComPort = false;
        private readonly Dictionary<UIComboBox, bool> _comboOpenStatus = new();
        private UIComboBox _currentOpenCombo = null;
        private readonly SwitchDevice _switchDevice;
        // 开关板原始接收缓存，用来拼接分片字节

        public Form1()
        {
            InitializeComponent();
            _switchDevice = new SwitchDevice();
            BindCombo(uiComboBox1);
            BindCombo(uiComboBox3);
            this.Load += Form1_Load;
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
        private void Form1_Load(object sender, EventArgs e)
        {
            // 界面加载完成、控件已经创建完毕，在这里刷新串口下拉框
            _ = RefreshAllSerialPortCombosAsync();
            LoadPlugins();
            var cbos = new UIComboBox[] { SwitchBaudrate, uiComboBox4 };

            foreach (var cbo in cbos)
            {
                if (cbo.Items.Count > 0)
                {
                    cbo.SelectedIndex = 0;
                }
            }
            comboBox1.SelectedIndex = 0;
            // 订阅设备业务事件（IO后台线程，UI必须Invoke）
            // 订阅设备业务事件（IO后台线程，UI必须Invoke）
            _switchDevice.OnDataReceived += SwitchDevice_OnDataReceived;
            _switchDevice.OnDisconnected += SwitchDevice_OnDisconnected;
            _switchDevice.OnError += SwitchDevice_OnError;
            //====新增订阅完整帧事件====
            _switchDevice.OnDeviceFrameComplete += SwitchDevice_OnDeviceFrameComplete;

            SerialGlobalManager.SerialLogEvent += OnSerialLog;
            if (SwitchBaudrate.Items.Count > 0)
            {
                SwitchBaudrate.SelectedIndex = 0;  // 代码可以写，属性面板看不见
            }
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

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 解绑本窗体UI事件
            _switchDevice.OnDataReceived -= SwitchDevice_OnDataReceived;
            _switchDevice.OnDisconnected -= SwitchDevice_OnDisconnected;
            SerialGlobalManager.SerialLogEvent -= OnSerialLog;
            _switchDevice.OnDeviceFrameComplete -= SwitchDevice_OnDeviceFrameComplete;
            _switchDevice.OnError -= SwitchDevice_OnError;
            // 2.释放全部串口硬件资源
            SerialGlobalManager.DisposeAllPorts();
        }
        private void uiButton2_Click(object sender, EventArgs e)
        {

        }

        private void uiGroupBox3_Click(object sender, EventArgs e)
        {

        }

        private void uiButton6_Click(object sender, EventArgs e)
        {

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
                string? lastSel2 = (uiComboBox3.SelectedItem as ComPortItem)?.ComPort;

                uiComboBox1.Items.Clear();
                uiComboBox3.Items.Clear();

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
                    uiComboBox3.Items.Add(item);
                }

                // 恢复上次选中：按ComPort匹配，不受显示文字、[已连接]标签影响
                foreach (var cbo in new[] { uiComboBox1, uiComboBox3 })
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
        private void uiComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

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

        private void uiButton3_Click(object sender, EventArgs e)
        {
            // 1. 获取所有勾选的 CheckBox
            var checkedBoxes = GetCheckedBoxes(uiGroupBox3); // 假设你的容器叫 uiPanel1

            // 2. 校验是否至少选择了一项
            if (checkedBoxes.Count == 0)
            {
                MessageBox.Show("请至少选择一个测试项！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            // 3. 统计数量并输出日志
            Console.WriteLine($"总共选中了 {checkedBoxes.Count} 项");
            // 4.通过测试盒筛选归零值
            var TestBoxSn = uiTextBox5.Text.Trim();
            String DutSn = uiTextBox2.Text.Trim();
            String DutType = comboBox1.Text.Trim();
            double zeroSupplement = double.Parse(uiTextBox3.Text.Trim());
            TestBoxZeroEntry entry = ZeroConfigManager.GetOrCreateBoxEntry(TestBoxSn);
            List<ZeroCalibration> Items = entry.Items;
            // 4. 遍历执行测试逻辑
            foreach (var checkBox in checkedBoxes)
            {
                string itemName = checkBox.Text; // 获取显示的文本
                ZeroCalibration? item = Items.FirstOrDefault(x => x.TestItemName == itemName);
                if (item == null)
                {
                    Console.WriteLine($"未找到测试盒 {TestBoxSn} 的归零项 {itemName}，请检查输入的测试盒序列号是否正确是否存在于Config/zero_calibration.json。");
                }
                Console.WriteLine($"{itemName} + {item?.TestItemName}");
                Console.WriteLine($"正在测试: {itemName}&&{item.TestItemId} ");
                // 执行对应的测试方法...
                //1.根据测试名 寻找对应开关状态，并执行开关操作
                List<String> switchStatus = item?.SWStatus ?? new List<string>();

                //**待补充//


                //2.采集光功率，并计算测试结果
                double zeroValue = item?.ZeroValueDbm ?? 0.0;
                double power = 0.0;
                //Global.opticalPowermeter.ReadPowerAsync().ContinueWith(t =>
                //{
                //    if (t.IsCompletedSuccessfully)
                //    {
                //        power = t.Result;
                //        MessageBox.Show($"功率读取成功: {power:F4} dBm", "信息");
                //    }
                //    else
                //    {
                //        MessageBox.Show($"功率读取失败: {t.Exception?.GetBaseException().Message}", "错误");
                //    }
                //}, TaskScheduler.FromCurrentSynchronizationContext());

                //2.1 IL需要计算其他不用，需要做判断； 2.1.1 IL计算的时候还需要判断器件类型，确定需不需要再减去一个归零值
                //IL = power - zeroValue - collimatorLoss
                double collimatorLoss = double.Parse(uiTextBox3.Text.Trim());
                double IL = 0.0;
                IL = comboBox1.Text.Trim().Equals("自由空间类") ? power - zeroValue - collimatorLoss : power - zeroValue;
                //3.填入测试结果到excel表里
                TestResultExcelWriter.AppendResult(new TestResultRow
                {
                    DutSn = DutSn,
                    DutType = DutType,
                    TestItemName = item.TestItemId + "-" + item.TestItemName,
                    ZeroValue = zeroValue,
                    ZeroSupplement = zeroSupplement,
                    PowerMeterReading = power,
                    TestResult = IL,
                    ZeroDate = item.CalibratedAt,
                }, "zcc");
                Console.WriteLine("写入成功");
            }
        }

        private List<UICheckBox> GetCheckedBoxes(Control container)
        {
            var result = new List<UICheckBox>();
            foreach (Control ctrl in container.Controls)
            {
                if (ctrl is UICheckBox cb && cb.Checked)
                {
                    result.Add(cb);
                }
            }
            return result.OrderBy(cb => cb.Top).ToList();
        }

        private void uiButton1_Click(object sender, EventArgs e)
        {

        }
        private UIRadioButton GetSelectedRadioButton(Control container)
        {
            foreach (var ctrl in container.Controls)
            {
                if (ctrl is UIRadioButton rb && rb.Checked)
                    return rb;
            }
            return null;
        }
        private void UpdateMarkLabel(string text)
        {
            if (uiMarkLabel1.InvokeRequired)
            {
                uiMarkLabel1.BeginInvoke(new Action(() => UpdateMarkLabel(text)));
            }
            else
            {
                uiMarkLabel1.Text = text;
            }
        }
        private async void uiButton1_Click_1(object sender, EventArgs e)
        {
            try
            {
                //_lblStatus.Text = "正在连接...";

                //var model = _cmbModel.SelectedItem?.ToString();
                //if (string.IsNullOrEmpty(model))
                //{
                //    MessageBox.Show("请选择设备型号", "提示");
                //    return;
                //}

                //var plugin = _pluginManager.FindPlugin(model);
                //if (plugin == null)
                //{
                //    MessageBox.Show($"未找到支持 {model} 的插件", "错误");
                //    return;
                //}

                //var transport = CreateTransport();
                //var protocol = CreateProtocol();

                //await transport.OpenAsync(_txtConnectionString.Text);

                //_currentDevice = plugin.Create(transport, protocol, new Dictionary<string, string>
                //{
                //    ["model"] = model
                //});

                //await _currentDevice.ConnectAsync(_txtConnectionString.Text);

                //// 更新 UI 状态
                //_btnConnect.Enabled = false;
                //_btnDisconnect.Enabled = true;
                //_btnReadPower.Enabled = true;
                //_btnSetWavelength.Enabled = true;
                //_btnZero.Enabled = true;
                //_txtWavelength.Enabled = true;
                //_lblStatus.Text = $"已连接: {_currentDevice.Info}";

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
                var transport = CreateTransport();
                Console.WriteLine($"[DEBUG] 创建传输协议成功: {transport.GetType().Name}");
                // ★ 调用插件给的工厂，MainForm 不 new 任何协议
                var protocol = CreateProtocol();
                Console.WriteLine($"[DEBUG] 找到插件: {plugin?.GetType().Name} 协议: {protocolName} 传输: {transportName}");
                //_currentDevice = plugin.Create(transport, protocol, new Dictionary<string, string>());
                await transport.OpenAsync(Omtransportparams.Text);

                _currentDevice = plugin.Create(transport, protocol, new Dictionary<string, string>
                {
                    ["model"] = model
                });
                Global.opticalPowermeter = _currentDevice;
                Console.WriteLine($"[DEBUG] 创建设备实例: {_currentDevice.GetType().FullName}+{protocol.GetType().Name}");
                Console.WriteLine($"[DEBUG] 打开成功: {_currentDevice.GetType().FullName}+{protocol.GetType().Name}");
                await _currentDevice.ConnectAsync(Omtransportparams.Text);
                //await _currentDevice.ReadPowerAsync();
                await _currentDevice.ReadPowerAsync().ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully)
                    {
                        var power = t.Result;
                        MessageBox.Show($"功率读取成功: {power:F4} dBm", "信息");
                    }
                    else
                    {
                        MessageBox.Show($"功率读取失败: {t.Exception?.GetBaseException().Message}", "错误");
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
            catch (Exception ex)
            {
                //_lblStatus.Text = "连接失败";
                MessageBox.Show($"连接失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void uiGroupBox1_Click(object sender, EventArgs e)
        {

        }

        private void uiGroupBox1_Click_1(object sender, EventArgs e)
        {

        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void uiGroupBox2_Click(object sender, EventArgs e)
        {

        }
        //加载插件
        private void LoadPlugins()
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

            //_pluginManager.ScanAndLoad(pluginsDir);

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

        private void Ommodel_SelectedIndexChanged(object sender, EventArgs e)
        {
            var model = Ommodel.SelectedItem?.ToString();
            if (Ommodel.SelectedIndex < 0 || Ommodel.Items.Count == 0)
                return;
            if (model == null) return;

            var plugin = _pluginManager.FindPlugin(model);
            if (plugin == null) return;

            Omtransport.Items.Clear();
            Omprotocol.Items.Clear();

            // 【修改】直接用 SupportedProtocols 填充协议下拉框
            foreach (var protocol in plugin.SupportedProtocols)
            {
                Omprotocol.Items.Add(protocol);
                Console.WriteLine($"[DEBUG] 型号 '{model}' 支持协议: {protocol}");
            }
            foreach (var transport in plugin.SupportedTransports)
            {
                Omtransport.Items.Add(transport);
                Console.WriteLine($"[DEBUG] 型号 '{model}' 支持传输方式: {transport}");
            }

            if (Omprotocol.Items.Count > 0)
            {
                Omprotocol.SelectedIndex = 0;
            }
            if (Omtransport.Items.Count > 0)
            {
                Omtransport.SelectedIndex = 0;
            }
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
            try
            {
                if (_currentDevice != null)
                {
                    await _currentDevice.DisconnectAsync();
                    _currentDevice.Dispose();
                    _currentDevice = null;
                    MessageBox.Show("设备已断开连接", "信息");
                }

                //_btnConnect.Enabled = true;
                //_btnDisconnect.Enabled = false;
                //_btnReadPower.Enabled = false;
                //_btnSetWavelength.Enabled = false;
                //_btnZero.Enabled = false;
                //_txtWavelength.Enabled = false;
                //_lblPower.Text = "-- dBm";
                //_lblStatus.Text = "已断开";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"断开失败: {ex.Message}", "错误");
            }
        }

        private void TestZero_Click(object sender, EventArgs e)
        {
            //1.通过检测测试项、切换不同的开关状态
            //1.1判断选择项
            Console.WriteLine($"Selected Test Item: {uiCheckBoxGroup3.SelectedIndexes}");
            var selected = GetSelectedRadioButton(uiCheckBoxGroup3);
            if (selected != null)
            {
                Console.WriteLine($"Text: {selected.Text}");
            }

            // 获取某台盒子全部归零项
            // 如果有对应测试盒则获取归零项
            //{
            //      "TestItemId": 1,
            //      "TestItemName": "Tx-Com(IL)",
            //      "Channel": "CH1",
            //      "ZeroValueDbm": -1.235,
            //      "CalibratedAt": "2026-08-18T09:00:00",
            //      "SWStatus": ["1", "1", "2", "0", "0"]
            //} 
            var TestBoxSn = uiTextBox4.Text.Trim();
            Console.WriteLine($"Test Box SN: {TestBoxSn}");
            TestBoxZeroEntry entry = ZeroConfigManager.GetOrCreateBoxEntry(TestBoxSn);
            List<ZeroCalibration> Items = entry.Items;
            ZeroCalibration? item = Items.FirstOrDefault(x => x.TestItemName == selected.Text);
            //查不到测试盒就提示 然后不再执行
            if (item == null)
            {
                MessageBox.Show($"未找到测试盒 {TestBoxSn} 的归零项，请检查输入的测试盒序列号是否正确是否存在于Config/zero_calibration.json。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            //1.2 根据测试项内容，查询本地json文件，获取对应的开关状态数组；
            List<String> switchStatus = item.SWStatus;
            //1.3 根据开关状态数组，执行对应的开关操作；
            //获取一下TestItemId，方便后续写入json文件
            String Powerchannel = item.Channel;
            Console.WriteLine($"TestId: {item.TestItemId}, TestName: {item.TestItemName} Switch Status for {selected.Text}: {string.Join(", ", switchStatus)}");
            //1.4 读取功率计读数，得到归零值，更新日期和归零值大小到本地json文件，更新状态UI
            //功率计通道暂时无法选择，手动修改配置表的命令可以选择读取的通道
            Global.opticalPowermeter.ReadPowerAsync().ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                {
                    var power = t.Result;
                    MessageBox.Show($"功率读取成功: {power:F4} dBm", "信息");
                    // 更新归零值到本地json文件
                    ZeroCalibration itemToUpdate = new ZeroCalibration
                    {
                        TestItemId = item.TestItemId,
                        TestItemName = selected.Text,
                        Channel = Powerchannel,
                        ZeroValueDbm = power,
                        CalibratedAt = DateTime.Now,
                        SWStatus = switchStatus
                    };
                    ZeroConfigManager.UpdateItem(TestBoxSn, itemToUpdate);
                }
                else
                {
                    MessageBox.Show($"功率读取失败: {t.Exception?.GetBaseException().Message}", "错误");
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void uiButton5_Click(object sender, EventArgs e)
        {

        }

        private void uiButton1_Click_2(object sender, EventArgs e)
        {
            UIForm frm = new UIForm();
            frm.Text = "图标预览";
            frm.Size = new Size(900, 650);
            FlowLayoutPanel flp = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true };
            frm.Controls.Add(flp);

            for (int i = 61440; i <= 62100; i++)
            {
                var lbl = new UISymbolLabel { Symbol = i, SymbolSize = 26, Text = $" {i}", Width = 130, Height = 44 };
                flp.Controls.Add(lbl);
            }
            frm.ShowDialog();
        }

        private void uiLabel15_Click(object sender, EventArgs e)
        {

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

        private async void uiButton9_Click(object sender, EventArgs e)
        {
            try
            {   
                String ch = switchCh.Text.Trim();
                int.TryParse(ch, out int value);
                //示例切通道3
                await _switchDevice.SwitchLightChannel(value);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"切换通道失败：{ex.Message}");
            }
        }
        // 使用
    }
}
