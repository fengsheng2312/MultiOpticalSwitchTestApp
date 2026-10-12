using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Sunny.UI;

namespace MultiOpticalSwitchTestApp
{
    /// <summary>
    /// 光源轮询测试结果展示窗（非模态）。
    /// 打开时不自动执行：点击“开始轮询”后主窗体才启动后台切换与读数；点击“停止轮询”结束。
    /// 归零值列可由用户手工修改，轮询线程检测到与 zeroDiff.json 不一致时会写回配置文件。
    /// </summary>
    public class SwitchPollForm : UIForm
    {
        private readonly DataGridView _grid;
        private readonly UIButton _btnStart;
        private readonly UIButton _btnStop;
        private readonly UILabel _statusLabel;
        private readonly UILabel _waveLabel;
        private readonly Dictionary<string, DataGridViewRow> _rows =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, double> _lastValidZero = new();

        /// <summary>一条轮询数据：光源序号、归零值、core、开关名、通道。</summary>
        public record PollItem(string LightNo, double ZeroValue, string Core, string SwitchName, int Channel);

        /// <summary>点击“开始轮询”。</summary>
        public event EventHandler? StartRequested;
        /// <summary>点击“停止轮询”。</summary>
        public event EventHandler? StopRequested;

        public SwitchPollForm(string waveLen, IEnumerable<PollItem> items)
        {
            Text = $"光源轮询测试 - 波长:{waveLen} nm";
            Name = "SwitchPollForm";
            Style = UIStyle.Green;
            Size = new Size(900, 620);
            MinimumSize = new Size(640, 360);
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = true;
            ShowIcon = false;
            ShowInTaskbar = true;

            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 56 };

            // 表格上方波长标题：整套数据均取自该波长节点
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 34 };
            _waveLabel = new UILabel
            {
                Text = $"波长:{waveLen} nm",
                Font = new Font("宋体", 12F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(12, 6),
                ForeColor = Color.FromArgb(48, 48, 48),
            };
            topPanel.Controls.Add(_waveLabel);

            _btnStart = new UIButton
            {
                Text = "开始轮询",
                Size = new Size(100, 34),
                Location = new Point(16, 11),
            };
            _btnStart.Click += (_, _) => StartRequested?.Invoke(this, EventArgs.Empty);

            _btnStop = new UIButton
            {
                Text = "停止轮询",
                Size = new Size(100, 34),
                Location = new Point(132, 11),
                Enabled = false,
            };
            _btnStop.Click += (_, _) => StopRequested?.Invoke(this, EventArgs.Empty);

            _statusLabel = new UILabel
            {
                Text = "未运行",
                Font = new Font("宋体", 10F),
                AutoSize = true,
                Location = new Point(252, 19),
                ForeColor = Color.FromArgb(48, 48, 48),
            };

            bottomPanel.Controls.Add(_btnStart);
            bottomPanel.Controls.Add(_btnStop);
            bottomPanel.Controls.Add(_statusLabel);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                Font = new Font("宋体", 11F),
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft },
            };

            AddColumn("colNo", "光源", readOnly: true);
            AddColumn("colZero", "归零值差值", readOnly: false);   // 允许手工修改
            AddColumn("colCore", "备注(core)", readOnly: true);
            AddColumn("colSW", "SW", readOnly: true);
            AddColumn("colCh", "Ch", readOnly: true);
            AddColumn("colPower", "功率计显示值", readOnly: true);
            AddColumn("colActual", "实际值", readOnly: true);
            _grid.Columns["colNo"].FillWeight = 60;
            _grid.Columns["colZero"].FillWeight = 80;
            _grid.Columns["colCore"].FillWeight = 90;
            _grid.Columns["colSW"].FillWeight = 90;
            _grid.Columns["colCh"].FillWeight = 50;
            _grid.Columns["colPower"].FillWeight = 120;
            _grid.Columns["colActual"].FillWeight = 100;

            foreach (var it in items)
            {
                int idx = _grid.Rows.Add(
                    it.LightNo,
                    it.ZeroValue.ToString("F4"),
                    string.IsNullOrWhiteSpace(it.Core) ? "/" : it.Core,
                    it.SwitchName,
                    it.Channel,
                    "-",
                    "-");
                var row = _grid.Rows[idx];
                row.Cells["colZero"].Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
                row.Cells["colPower"].Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
                row.Cells["colActual"].Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
                _rows[it.LightNo] = row;
            }

            // 归零值输入校验：非法内容回退到上一次的合法值
            _grid.CellParsing += (_, e) =>
            {
                if (e.RowIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "colZero") return;
                string text = Convert.ToString(e.Value, CultureInfo.InvariantCulture) ?? string.Empty;
                if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                {
                    e.Value = _lastValidZero.TryGetValue(e.RowIndex, out double old) ? (object)old : 0.0;
                }
                else
                {
                    _lastValidZero[e.RowIndex] = v;
                }
            };

            Controls.Add(_grid);
            Controls.Add(topPanel);
            Controls.Add(bottomPanel);
        }

        private void AddColumn(string name, string header, bool readOnly)
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                ReadOnly = readOnly,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleLeft
                }
            });
        }

        /// <summary>
        /// 设置运行状态：开始/停止按钮互斥启用，底部状态文字提示。
        /// 主线程直接调用时 invokeOnUi:false；后台线程调用走默认跨线程分支。
        /// </summary>
        public void SetRunning(bool running, bool invokeOnUi = true)
        {
            if (IsDisposed) return;
            if (invokeOnUi && InvokeRequired)
            {
                try { BeginInvoke(new Action(() => SetRunning(running, false))); }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
                return;
            }
            _btnStart.Enabled = !running;
            _btnStop.Enabled = running;
            _statusLabel.Text = running
                ? $"运行中  {DateTime.Now:HH:mm:ss}"
                : "未运行";
        }

        /// <summary>读取指定光源行当前的归零值（线程安全）；内容非法时回退 0。</summary>
        public double GetZeroValue(string lightNo)
        {
            if (IsDisposed) return 0.0;
            if (InvokeRequired)
            {
                return (double)Invoke(new Func<double>(() => GetZeroValueCore(lightNo)));
            }
            return GetZeroValueCore(lightNo);
        }

        private double GetZeroValueCore(string lightNo)
        {
            if (!_rows.TryGetValue(lightNo, out var row)) return 0.0;
            string text = Convert.ToString(row.Cells["colZero"].Value, CultureInfo.InvariantCulture) ?? string.Empty;
            return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
                ? v
                : 0.0;
        }

        /// <summary>刷新指定光源行的功率计显示值与实际值（线程安全）。</summary>
        public void UpdateResult(string lightNo, string powerText, string actualText)
        {
            if (IsDisposed) return;
            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => UpdateResult(lightNo, powerText, actualText)));
                    return;
                }
                if (_rows.TryGetValue(lightNo, out var row))
                {
                    row.Cells["colPower"].Value = powerText;
                    row.Cells["colActual"].Value = actualText;
                }
            }
            catch (ObjectDisposedException)
            {
                // 窗体关闭竞态，忽略
            }
            catch (InvalidOperationException)
            {
                // 句柄已销毁竞态，忽略
            }
        }
    }
}
