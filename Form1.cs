using Guna.UI2.WinForms;

namespace Free_spaceOpticalCirculatorTestApp
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            Size = new Size(900, 600);
            Text = "Guna2‑Form1示例";

            // 无边框+窗体阴影
            Guna2ShadowForm shadowForm = new Guna2ShadowForm(this);
            shadowForm.SetShadowForm(this);
            FormBorderStyle = FormBorderStyle.None;

            // 无边框窗口拖拽
            Guna2DragControl dragControl = new Guna2DragControl();
            dragControl.TargetControl = this;

            // 1.圆角按钮
            Guna2Button btnTest = new Guna2Button();
            btnTest.Location = new Point(30, 30);
            btnTest.Size = new Size(160, 45);
            btnTest.Text = "Guna按钮";
            btnTest.BorderRadius = 12;
            btnTest.FillColor = Color.FromArgb(25, 135, 220);
            btnTest.HoverState.FillColor = Color.FromArgb(15, 110, 190);
            btnTest.Click += BtnTest_Click;
            this.Controls.Add(btnTest);

            // 2.圆角输入框
            Guna2TextBox txtInput = new Guna2TextBox();
            txtInput.Location = new Point(30, 100);
            txtInput.Size = new Size(240, 40);
            txtInput.PlaceholderText = "请输入内容";
            txtInput.BorderRadius = 8;
            txtInput.FocusedState.BorderColor = Color.FromArgb(25, 135, 220);
            this.Controls.Add(txtInput);

            // 3.开关
            Guna2ToggleSwitch toggle = new Guna2ToggleSwitch();
            toggle.Location = new Point(30, 160);
            toggle.Size = new Size(60, 30);
            toggle.Checked = false;
            toggle.CheckedChanged += (s, e) =>
            {
                MessageBox.Show($"开关状态：{toggle.Checked}");
            };
            this.Controls.Add(toggle);

            // 4.环形进度条
            Guna2CircleProgressBar circleProg = new Guna2CircleProgressBar();
            circleProg.Location = new Point(300, 30);
            circleProg.Size = new Size(150, 150);
            circleProg.Value = 65;
            circleProg.ProgressColor = Color.DodgerBlue;
            circleProg.ProgressThickness = 12;
            this.Controls.Add(circleProg);

            // 5.数字输入框
            Guna2NumericUpDown numUpDown = new Guna2NumericUpDown();
            numUpDown.Location = new Point(30, 220);
            numUpDown.Size = new Size(140, 40);
            numUpDown.Minimum = 0;
            numUpDown.Maximum = 1000;
            numUpDown.Value = 100;
            numUpDown.BorderRadius = 8;
            this.Controls.Add(numUpDown);

            // 6.美化表格
            Guna2DataGridView dgv = new Guna2DataGridView();
            dgv.Location = new Point(30, 280);
            dgv.Size = new Size(820, 250);
            dgv.AllowUserToAddRows = true;
            dgv.ColumnCount = 2;
            dgv.Columns[0].Name = "ID";
            dgv.Columns[1].Name = "描述";
            dgv.Rows.Add("1", "上位机测试");
            dgv.Rows.Add("2", "光通信设备");
            this.Controls.Add(dgv);
        }

        private void BtnTest_Click(object? sender, EventArgs e)
        {
            Guna2MessageDialog msg = new Guna2MessageDialog();
            msg.Text = "来自GunaUI弹窗";
            msg.Caption = "提示";
            msg.Buttons = MessageDialogButtons.OK;
            msg.Icon = MessageDialogIcon.Information;
            msg.Show();
        }
    }
}
