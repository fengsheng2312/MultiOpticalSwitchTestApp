using Sunny.UI;
namespace MultiOpticalSwitchTestApp
{
    partial class Form1:UIForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            uiGroupBox1 = new UIGroupBox();
            uiTableLayoutPanel6 = new UITableLayoutPanel();
            uiTableLayoutPanel5 = new UITableLayoutPanel();
            uiTableLayoutPanel4 = new UITableLayoutPanel();
            uiTableLayoutPanel3 = new UITableLayoutPanel();
            uiTableLayoutPanel2 = new UITableLayoutPanel();
            uiTableLayoutPanel1 = new UITableLayoutPanel();
            uiPanel3 = new UIPanel();
            uiFlowLayoutPanel3 = new UIFlowLayoutPanel();
            uiCheckBoxGroup1 = new UICheckBoxGroup();
            uiTableLayoutPanel7 = new UITableLayoutPanel();
            checkList = new UITextBox();
            uiLabel7 = new UILabel();
            waveLenText = new UITextBox();
            uiLabel2 = new UILabel();
            checkAll = new UIButton();
            uiLabel10 = new UILabel();
            uiLabel8 = new UILabel();
            uiLabel6 = new UILabel();
            baudrate = new UIComboBox();
            uiLabel3 = new UILabel();
            switchCom = new UIComboBox();
            uiComboBox2 = new UIComboBox();
            uiLabel24 = new UILabel();
            uiLabel20 = new UILabel();
            uiTextBox1 = new UITextBox();
            uiButton7 = new UIButton();
            uiButton5 = new UIButton();
            uiLabel14 = new UILabel();
            uiLabel13 = new UILabel();
            Ommodel = new UIComboBox();
            uiLabel12 = new UILabel();
            Omprotocol = new UIComboBox();
            uiLabel11 = new UILabel();
            Omtransport = new UIComboBox();
            connectButton = new UIButton();
            uiGroupBox2 = new UIGroupBox();
            uiSymbolLabel7 = new UISymbolLabel();
            uiSymbolLabel1 = new UISymbolLabel();
            uiButton14 = new UIButton();
            uiLabel19 = new UILabel();
            rtb_Log = new UIRichTextBox();
            uiButton8 = new UIButton();
            uiTextBox7 = new UITextBox();
            uiLabel5 = new UILabel();
            uiLabel1 = new UILabel();
            uiButton6 = new UIButton();
            uiComboBox1 = new UIComboBox();
            uiLabel4 = new UILabel();
            uiLight1 = new UILight();
            SwitchBaudrate = new UIComboBox();
            uiButton2 = new UIButton();
            uiGroupBox1.SuspendLayout();
            uiPanel3.SuspendLayout();
            uiFlowLayoutPanel3.SuspendLayout();
            uiCheckBoxGroup1.SuspendLayout();
            uiTableLayoutPanel7.SuspendLayout();
            uiGroupBox2.SuspendLayout();
            SuspendLayout();
            // 
            // uiGroupBox1
            // 
            uiGroupBox1.Controls.Add(uiTableLayoutPanel6);
            uiGroupBox1.Controls.Add(uiTableLayoutPanel5);
            uiGroupBox1.Controls.Add(uiTableLayoutPanel4);
            uiGroupBox1.Controls.Add(uiTableLayoutPanel3);
            uiGroupBox1.Controls.Add(uiTableLayoutPanel2);
            uiGroupBox1.Controls.Add(uiTableLayoutPanel1);
            uiGroupBox1.Controls.Add(uiPanel3);
            uiGroupBox1.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiGroupBox1.Location = new Point(329, 40);
            uiGroupBox1.Margin = new Padding(4, 5, 4, 5);
            uiGroupBox1.MinimumSize = new Size(1, 1);
            uiGroupBox1.Name = "uiGroupBox1";
            uiGroupBox1.Padding = new Padding(0, 32, 0, 0);
            uiGroupBox1.Size = new Size(1212, 817);
            uiGroupBox1.TabIndex = 1;
            uiGroupBox1.Text = "Mems光开关切换";
            uiGroupBox1.TextAlignment = ContentAlignment.MiddleLeft;
            uiGroupBox1.Click += uiGroupBox1_Click_1;
            // 
            // uiTableLayoutPanel6
            // 
            uiTableLayoutPanel6.ColumnCount = 2;
            uiTableLayoutPanel6.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            uiTableLayoutPanel6.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            uiTableLayoutPanel6.Location = new Point(805, 468);
            uiTableLayoutPanel6.Name = "uiTableLayoutPanel6";
            uiTableLayoutPanel6.RowCount = 2;
            uiTableLayoutPanel6.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            uiTableLayoutPanel6.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            uiTableLayoutPanel6.Size = new Size(384, 202);
            uiTableLayoutPanel6.TabIndex = 37;
            uiTableLayoutPanel6.TagString = null;
            // 
            // uiTableLayoutPanel5
            // 
            uiTableLayoutPanel5.ColumnCount = 2;
            uiTableLayoutPanel5.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            uiTableLayoutPanel5.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            uiTableLayoutPanel5.Location = new Point(359, 459);
            uiTableLayoutPanel5.Name = "uiTableLayoutPanel5";
            uiTableLayoutPanel5.RowCount = 2;
            uiTableLayoutPanel5.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            uiTableLayoutPanel5.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            uiTableLayoutPanel5.Size = new Size(396, 202);
            uiTableLayoutPanel5.TabIndex = 36;
            uiTableLayoutPanel5.TagString = null;
            // 
            // uiTableLayoutPanel4
            // 
            uiTableLayoutPanel4.ColumnCount = 2;
            uiTableLayoutPanel4.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            uiTableLayoutPanel4.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            uiTableLayoutPanel4.Location = new Point(805, 251);
            uiTableLayoutPanel4.Name = "uiTableLayoutPanel4";
            uiTableLayoutPanel4.RowCount = 2;
            uiTableLayoutPanel4.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            uiTableLayoutPanel4.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            uiTableLayoutPanel4.Size = new Size(384, 202);
            uiTableLayoutPanel4.TabIndex = 35;
            uiTableLayoutPanel4.TagString = null;
            // 
            // uiTableLayoutPanel3
            // 
            uiTableLayoutPanel3.ColumnCount = 2;
            uiTableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            uiTableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            uiTableLayoutPanel3.Location = new Point(359, 251);
            uiTableLayoutPanel3.Name = "uiTableLayoutPanel3";
            uiTableLayoutPanel3.RowCount = 2;
            uiTableLayoutPanel3.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            uiTableLayoutPanel3.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            uiTableLayoutPanel3.Size = new Size(396, 202);
            uiTableLayoutPanel3.TabIndex = 34;
            uiTableLayoutPanel3.TagString = null;
            // 
            // uiTableLayoutPanel2
            // 
            uiTableLayoutPanel2.ColumnCount = 2;
            uiTableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            uiTableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            uiTableLayoutPanel2.Location = new Point(805, 43);
            uiTableLayoutPanel2.Name = "uiTableLayoutPanel2";
            uiTableLayoutPanel2.RowCount = 2;
            uiTableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            uiTableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            uiTableLayoutPanel2.Size = new Size(384, 202);
            uiTableLayoutPanel2.TabIndex = 33;
            uiTableLayoutPanel2.TagString = null;
            // 
            // uiTableLayoutPanel1
            // 
            uiTableLayoutPanel1.ColumnCount = 2;
            uiTableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            uiTableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            uiTableLayoutPanel1.Location = new Point(359, 43);
            uiTableLayoutPanel1.Name = "uiTableLayoutPanel1";
            uiTableLayoutPanel1.RowCount = 2;
            uiTableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            uiTableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            uiTableLayoutPanel1.Size = new Size(396, 202);
            uiTableLayoutPanel1.TabIndex = 32;
            uiTableLayoutPanel1.TagString = null;
            // 
            // uiPanel3
            // 
            uiPanel3.Controls.Add(uiFlowLayoutPanel3);
            uiPanel3.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiPanel3.Location = new Point(18, 31);
            uiPanel3.Margin = new Padding(4, 5, 4, 5);
            uiPanel3.MinimumSize = new Size(1, 1);
            uiPanel3.Name = "uiPanel3";
            uiPanel3.RectColor = Color.White;
            uiPanel3.Size = new Size(334, 765);
            uiPanel3.TabIndex = 31;
            uiPanel3.Text = "uiPanel3";
            uiPanel3.TextAlignment = ContentAlignment.MiddleCenter;
            // 
            // uiFlowLayoutPanel3
            // 
            uiFlowLayoutPanel3.Controls.Add(uiCheckBoxGroup1);
            uiFlowLayoutPanel3.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiFlowLayoutPanel3.Location = new Point(4, 5);
            uiFlowLayoutPanel3.Margin = new Padding(4, 5, 4, 5);
            uiFlowLayoutPanel3.MinimumSize = new Size(1, 1);
            uiFlowLayoutPanel3.Name = "uiFlowLayoutPanel3";
            uiFlowLayoutPanel3.Padding = new Padding(2);
            uiFlowLayoutPanel3.ShowText = false;
            uiFlowLayoutPanel3.Size = new Size(315, 760);
            uiFlowLayoutPanel3.TabIndex = 0;
            uiFlowLayoutPanel3.Text = "uiFlowLayoutPanel3";
            uiFlowLayoutPanel3.TextAlignment = ContentAlignment.MiddleCenter;
            // 
            // uiCheckBoxGroup1
            // 
            uiCheckBoxGroup1.Controls.Add(uiTableLayoutPanel7);
            uiCheckBoxGroup1.Controls.Add(uiLabel6);
            uiCheckBoxGroup1.Controls.Add(baudrate);
            uiCheckBoxGroup1.Controls.Add(uiLabel3);
            uiCheckBoxGroup1.Controls.Add(switchCom);
            uiCheckBoxGroup1.Controls.Add(uiComboBox2);
            uiCheckBoxGroup1.Controls.Add(uiLabel24);
            uiCheckBoxGroup1.Controls.Add(uiLabel20);
            uiCheckBoxGroup1.Controls.Add(uiTextBox1);
            uiCheckBoxGroup1.Controls.Add(uiButton7);
            uiCheckBoxGroup1.Controls.Add(uiButton5);
            uiCheckBoxGroup1.Controls.Add(uiLabel14);
            uiCheckBoxGroup1.Controls.Add(uiLabel13);
            uiCheckBoxGroup1.Controls.Add(Ommodel);
            uiCheckBoxGroup1.Controls.Add(uiLabel12);
            uiCheckBoxGroup1.Controls.Add(Omprotocol);
            uiCheckBoxGroup1.Controls.Add(uiLabel11);
            uiCheckBoxGroup1.Controls.Add(Omtransport);
            uiCheckBoxGroup1.Controls.Add(connectButton);
            uiCheckBoxGroup1.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiCheckBoxGroup1.Location = new Point(6, 7);
            uiCheckBoxGroup1.Margin = new Padding(4, 5, 4, 5);
            uiCheckBoxGroup1.MinimumSize = new Size(1, 1);
            uiCheckBoxGroup1.Name = "uiCheckBoxGroup1";
            uiCheckBoxGroup1.Padding = new Padding(0, 32, 0, 0);
            uiCheckBoxGroup1.RectColor = Color.White;
            uiCheckBoxGroup1.SelectedIndexes = (List<int>)resources.GetObject("uiCheckBoxGroup1.SelectedIndexes");
            uiCheckBoxGroup1.Size = new Size(303, 725);
            uiCheckBoxGroup1.TabIndex = 29;
            uiCheckBoxGroup1.Text = "功率计连接";
            uiCheckBoxGroup1.TextAlignment = ContentAlignment.MiddleLeft;
            uiCheckBoxGroup1.ValueChanged += uiCheckBoxGroup1_ValueChanged;
            // 
            // uiTableLayoutPanel7
            // 
            uiTableLayoutPanel7.Anchor = AnchorStyles.Top | AnchorStyles.Bottom;
            uiTableLayoutPanel7.ColumnCount = 3;
            uiTableLayoutPanel7.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 51.6853943F));
            uiTableLayoutPanel7.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48.3146057F));
            uiTableLayoutPanel7.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));
            uiTableLayoutPanel7.Controls.Add(checkList, 1, 1);
            uiTableLayoutPanel7.Controls.Add(uiLabel7, 2, 0);
            uiTableLayoutPanel7.Controls.Add(waveLenText, 1, 0);
            uiTableLayoutPanel7.Controls.Add(uiLabel2, 0, 0);
            uiTableLayoutPanel7.Controls.Add(checkAll, 0, 3);
            uiTableLayoutPanel7.Controls.Add(uiLabel10, 0, 2);
            uiTableLayoutPanel7.Controls.Add(uiLabel8, 0, 1);
            uiTableLayoutPanel7.Location = new Point(16, 486);
            uiTableLayoutPanel7.Name = "uiTableLayoutPanel7";
            uiTableLayoutPanel7.RowCount = 4;
            uiTableLayoutPanel7.RowStyles.Add(new RowStyle(SizeType.Percent, 39.02439F));
            uiTableLayoutPanel7.RowStyles.Add(new RowStyle(SizeType.Percent, 60.97561F));
            uiTableLayoutPanel7.RowStyles.Add(new RowStyle(SizeType.Absolute, 27F));
            uiTableLayoutPanel7.RowStyles.Add(new RowStyle(SizeType.Absolute, 85F));
            uiTableLayoutPanel7.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            uiTableLayoutPanel7.Size = new Size(275, 236);
            uiTableLayoutPanel7.TabIndex = 60;
            uiTableLayoutPanel7.TagString = null;
            // 
            // checkList
            // 
            uiTableLayoutPanel7.SetColumnSpan(checkList, 2);
            checkList.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            checkList.Location = new Point(96, 53);
            checkList.Margin = new Padding(4, 5, 4, 5);
            checkList.MinimumSize = new Size(1, 16);
            checkList.Name = "checkList";
            checkList.Padding = new Padding(5);
            checkList.ShowText = false;
            checkList.Size = new Size(175, 29);
            checkList.TabIndex = 61;
            checkList.Text = "1~15";
            checkList.TextAlignment = ContentAlignment.MiddleLeft;
            checkList.Watermark = "如 1~15/1,2,3";
            // 
            // uiLabel7
            // 
            uiLabel7.Anchor = AnchorStyles.None;
            uiLabel7.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiLabel7.ForeColor = Color.FromArgb(48, 48, 48);
            uiLabel7.Location = new Point(196, 12);
            uiLabel7.Name = "uiLabel7";
            uiLabel7.Size = new Size(60, 23);
            uiLabel7.TabIndex = 59;
            uiLabel7.Text = "nm";
            // 
            // waveLenText
            // 
            waveLenText.DoubleValue = 1310D;
            waveLenText.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            waveLenText.IntValue = 1310;
            waveLenText.Location = new Point(96, 5);
            waveLenText.Margin = new Padding(4, 5, 4, 5);
            waveLenText.MinimumSize = new Size(1, 16);
            waveLenText.Name = "waveLenText";
            waveLenText.Padding = new Padding(5);
            waveLenText.ShowText = false;
            waveLenText.Size = new Size(75, 31);
            waveLenText.TabIndex = 57;
            waveLenText.Text = "1310";
            waveLenText.TextAlignment = ContentAlignment.MiddleLeft;
            waveLenText.Watermark = "";
            // 
            // uiLabel2
            // 
            uiLabel2.Anchor = AnchorStyles.None;
            uiLabel2.Font = new Font("宋体", 10F);
            uiLabel2.ForeColor = Color.FromArgb(48, 48, 48);
            uiLabel2.Location = new Point(17, 12);
            uiLabel2.Margin = new Padding(3);
            uiLabel2.Name = "uiLabel2";
            uiLabel2.Size = new Size(57, 23);
            uiLabel2.TabIndex = 58;
            uiLabel2.Text = "波长：";
            // 
            // checkAll
            // 
            checkAll.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            checkAll.Location = new Point(3, 153);
            checkAll.MinimumSize = new Size(1, 1);
            checkAll.Name = "checkAll";
            checkAll.Size = new Size(83, 33);
            checkAll.TabIndex = 55;
            checkAll.Text = "全量轮询";
            checkAll.TipsFont = new Font("宋体", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            // 
            // uiLabel10
            // 
            uiTableLayoutPanel7.SetColumnSpan(uiLabel10, 3);
            uiLabel10.Font = new Font("宋体", 10F);
            uiLabel10.ForeColor = Color.FromArgb(48, 48, 48);
            uiLabel10.Location = new Point(3, 123);
            uiLabel10.Name = "uiLabel10";
            uiLabel10.Size = new Size(269, 23);
            uiLabel10.TabIndex = 63;
            uiLabel10.Text = "填写（如）：1~15/或逗号分隔1,3,5";
            // 
            // uiLabel8
            // 
            uiLabel8.Anchor = AnchorStyles.None;
            uiLabel8.Font = new Font("宋体", 10F);
            uiLabel8.ForeColor = Color.FromArgb(48, 48, 48);
            uiLabel8.Location = new Point(6, 59);
            uiLabel8.Margin = new Padding(3);
            uiLabel8.Name = "uiLabel8";
            uiLabel8.Size = new Size(79, 53);
            uiLabel8.TabIndex = 60;
            uiLabel8.Text = "轮询序号：";
            uiLabel8.Click += uiLabel8_Click;
            // 
            // uiLabel6
            // 
            uiLabel6.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiLabel6.ForeColor = Color.FromArgb(48, 48, 48);
            uiLabel6.Location = new Point(13, 223);
            uiLabel6.Margin = new Padding(0);
            uiLabel6.Name = "uiLabel6";
            uiLabel6.Size = new Size(63, 29);
            uiLabel6.TabIndex = 54;
            uiLabel6.Text = "波特率";
            uiLabel6.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // baudrate
            // 
            baudrate.DataSource = null;
            baudrate.FillColor = Color.White;
            baudrate.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            baudrate.ItemHoverColor = Color.FromArgb(155, 200, 255);
            baudrate.Items.AddRange(new object[] { "115200", "57600", "38400" });
            baudrate.ItemSelectForeColor = Color.FromArgb(235, 243, 255);
            baudrate.Location = new Point(98, 223);
            baudrate.Margin = new Padding(4, 5, 4, 5);
            baudrate.MinimumSize = new Size(63, 0);
            baudrate.Name = "baudrate";
            baudrate.Padding = new Padding(0, 0, 30, 2);
            baudrate.Size = new Size(116, 29);
            baudrate.SymbolSize = 24;
            baudrate.TabIndex = 53;
            baudrate.TextAlignment = ContentAlignment.MiddleLeft;
            baudrate.Watermark = "";
            // 
            // uiLabel3
            // 
            uiLabel3.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiLabel3.ForeColor = Color.FromArgb(48, 48, 48);
            uiLabel3.Location = new Point(7, 162);
            uiLabel3.Name = "uiLabel3";
            uiLabel3.Size = new Size(284, 23);
            uiLabel3.TabIndex = 52;
            uiLabel3.Text = "功率计串口";
            // 
            // switchCom
            // 
            switchCom.DataSource = null;
            switchCom.FillColor = Color.White;
            switchCom.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            switchCom.ItemHoverColor = Color.FromArgb(155, 200, 255);
            switchCom.ItemSelectForeColor = Color.FromArgb(235, 243, 255);
            switchCom.Location = new Point(10, 189);
            switchCom.Margin = new Padding(4, 5, 4, 5);
            switchCom.MinimumSize = new Size(63, 0);
            switchCom.Name = "switchCom";
            switchCom.Padding = new Padding(0, 0, 30, 2);
            switchCom.Size = new Size(281, 29);
            switchCom.SymbolSize = 24;
            switchCom.TabIndex = 51;
            switchCom.TextAlignment = ContentAlignment.MiddleLeft;
            switchCom.Watermark = "";
            // 
            // uiComboBox2
            // 
            uiComboBox2.DataSource = null;
            uiComboBox2.FillColor = Color.White;
            uiComboBox2.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiComboBox2.ItemHoverColor = Color.FromArgb(155, 200, 255);
            uiComboBox2.Items.AddRange(new object[] { "1", "2", "3", "4" });
            uiComboBox2.ItemSelectForeColor = Color.FromArgb(235, 243, 255);
            uiComboBox2.Location = new Point(98, 353);
            uiComboBox2.Margin = new Padding(4, 5, 4, 5);
            uiComboBox2.MinimumSize = new Size(63, 0);
            uiComboBox2.Name = "uiComboBox2";
            uiComboBox2.Padding = new Padding(0, 0, 30, 2);
            uiComboBox2.Size = new Size(132, 29);
            uiComboBox2.SymbolSize = 24;
            uiComboBox2.TabIndex = 50;
            uiComboBox2.TextAlignment = ContentAlignment.MiddleLeft;
            uiComboBox2.Watermark = "";
            uiComboBox2.SelectedIndexChanged += uiComboBox2_SelectedIndexChanged;
            // 
            // uiLabel24
            // 
            uiLabel24.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiLabel24.ForeColor = Color.FromArgb(48, 48, 48);
            uiLabel24.Location = new Point(7, 359);
            uiLabel24.Name = "uiLabel24";
            uiLabel24.Size = new Size(88, 23);
            uiLabel24.TabIndex = 49;
            uiLabel24.Text = "读取通道(dbm)：";
            // 
            // uiLabel20
            // 
            uiLabel20.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiLabel20.ForeColor = Color.FromArgb(48, 48, 48);
            uiLabel20.Location = new Point(11, 441);
            uiLabel20.Name = "uiLabel20";
            uiLabel20.Size = new Size(80, 23);
            uiLabel20.TabIndex = 42;
            uiLabel20.Text = "读数(dbm)：";
            // 
            // uiTextBox1
            // 
            uiTextBox1.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiTextBox1.Location = new Point(98, 435);
            uiTextBox1.Margin = new Padding(4, 5, 4, 5);
            uiTextBox1.MinimumSize = new Size(1, 16);
            uiTextBox1.Name = "uiTextBox1";
            uiTextBox1.Padding = new Padding(5);
            uiTextBox1.ShowText = false;
            uiTextBox1.Size = new Size(158, 29);
            uiTextBox1.TabIndex = 41;
            uiTextBox1.TextAlignment = ContentAlignment.MiddleLeft;
            uiTextBox1.Watermark = "";
            // 
            // uiButton7
            // 
            uiButton7.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiButton7.Location = new Point(129, 305);
            uiButton7.MinimumSize = new Size(1, 1);
            uiButton7.Name = "uiButton7";
            uiButton7.Size = new Size(80, 35);
            uiButton7.TabIndex = 35;
            uiButton7.Text = "断开";
            uiButton7.TipsFont = new Font("宋体", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiButton7.Click += uiButton7_Click;
            // 
            // uiButton5
            // 
            uiButton5.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiButton5.Location = new Point(11, 394);
            uiButton5.MinimumSize = new Size(1, 1);
            uiButton5.Name = "uiButton5";
            uiButton5.Size = new Size(83, 35);
            uiButton5.TabIndex = 33;
            uiButton5.Text = "读取功率";
            uiButton5.TipsFont = new Font("宋体", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiButton5.Click += uiButton5_Click;
            // 
            // uiLabel14
            // 
            uiLabel14.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiLabel14.ForeColor = Color.FromArgb(48, 48, 48);
            uiLabel14.Location = new Point(11, 120);
            uiLabel14.Name = "uiLabel14";
            uiLabel14.Size = new Size(90, 35);
            uiLabel14.TabIndex = 31;
            uiLabel14.Text = "连接参数：";
            uiLabel14.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // uiLabel13
            // 
            uiLabel13.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiLabel13.ForeColor = Color.FromArgb(48, 48, 48);
            uiLabel13.Location = new Point(10, 35);
            uiLabel13.Margin = new Padding(0);
            uiLabel13.Name = "uiLabel13";
            uiLabel13.Size = new Size(99, 29);
            uiLabel13.TabIndex = 27;
            uiLabel13.Text = "设备型号：";
            uiLabel13.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // Ommodel
            // 
            Ommodel.DataSource = null;
            Ommodel.FillColor = Color.White;
            Ommodel.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            Ommodel.ItemHoverColor = Color.FromArgb(155, 200, 255);
            Ommodel.ItemSelectForeColor = Color.FromArgb(235, 243, 255);
            Ommodel.Location = new Point(114, 37);
            Ommodel.Margin = new Padding(4, 5, 4, 5);
            Ommodel.MinimumSize = new Size(63, 0);
            Ommodel.Name = "Ommodel";
            Ommodel.Padding = new Padding(0, 0, 30, 2);
            Ommodel.Size = new Size(132, 29);
            Ommodel.SymbolSize = 24;
            Ommodel.TabIndex = 26;
            Ommodel.TextAlignment = ContentAlignment.MiddleLeft;
            Ommodel.Watermark = "";
            Ommodel.SelectedIndexChanged += Ommodel_SelectedIndexChanged;
            // 
            // uiLabel12
            // 
            uiLabel12.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiLabel12.ForeColor = Color.FromArgb(48, 48, 48);
            uiLabel12.Location = new Point(13, 262);
            uiLabel12.Margin = new Padding(0);
            uiLabel12.Name = "uiLabel12";
            uiLabel12.Size = new Size(75, 29);
            uiLabel12.TabIndex = 25;
            uiLabel12.Text = "协议：";
            uiLabel12.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // Omprotocol
            // 
            Omprotocol.DataSource = null;
            Omprotocol.FillColor = Color.White;
            Omprotocol.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            Omprotocol.ItemHoverColor = Color.FromArgb(155, 200, 255);
            Omprotocol.ItemSelectForeColor = Color.FromArgb(235, 243, 255);
            Omprotocol.Location = new Point(99, 262);
            Omprotocol.Margin = new Padding(4, 5, 4, 5);
            Omprotocol.MinimumSize = new Size(63, 0);
            Omprotocol.Name = "Omprotocol";
            Omprotocol.Padding = new Padding(0, 0, 30, 2);
            Omprotocol.Size = new Size(131, 29);
            Omprotocol.SymbolSize = 24;
            Omprotocol.TabIndex = 24;
            Omprotocol.TextAlignment = ContentAlignment.MiddleLeft;
            Omprotocol.Watermark = "";
            // 
            // uiLabel11
            // 
            uiLabel11.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiLabel11.ForeColor = Color.FromArgb(48, 48, 48);
            uiLabel11.Location = new Point(11, 86);
            uiLabel11.Margin = new Padding(0);
            uiLabel11.Name = "uiLabel11";
            uiLabel11.Size = new Size(90, 29);
            uiLabel11.TabIndex = 23;
            uiLabel11.Text = "传输方式：";
            uiLabel11.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // Omtransport
            // 
            Omtransport.DataSource = null;
            Omtransport.Enabled = false;
            Omtransport.FillColor = Color.White;
            Omtransport.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            Omtransport.ItemHoverColor = Color.FromArgb(155, 200, 255);
            Omtransport.ItemSelectForeColor = Color.FromArgb(235, 243, 255);
            Omtransport.Location = new Point(114, 86);
            Omtransport.Margin = new Padding(4, 5, 4, 5);
            Omtransport.MinimumSize = new Size(63, 0);
            Omtransport.Name = "Omtransport";
            Omtransport.Padding = new Padding(0, 0, 30, 2);
            Omtransport.Size = new Size(132, 29);
            Omtransport.SymbolSize = 24;
            Omtransport.TabIndex = 22;
            Omtransport.Text = "Serial";
            Omtransport.TextAlignment = ContentAlignment.MiddleLeft;
            Omtransport.Watermark = "";
            // 
            // connectButton
            // 
            connectButton.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            connectButton.Location = new Point(13, 305);
            connectButton.MinimumSize = new Size(1, 1);
            connectButton.Name = "connectButton";
            connectButton.Size = new Size(83, 35);
            connectButton.TabIndex = 25;
            connectButton.Text = "连接";
            connectButton.TipsFont = new Font("宋体", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            connectButton.Click += uiButton1_Click_1;
            // 
            // uiGroupBox2
            // 
            uiGroupBox2.Controls.Add(uiSymbolLabel7);
            uiGroupBox2.Controls.Add(uiSymbolLabel1);
            uiGroupBox2.Controls.Add(uiButton14);
            uiGroupBox2.Controls.Add(uiLabel19);
            uiGroupBox2.Controls.Add(rtb_Log);
            uiGroupBox2.Controls.Add(uiButton8);
            uiGroupBox2.Controls.Add(uiTextBox7);
            uiGroupBox2.Controls.Add(uiLabel5);
            uiGroupBox2.Controls.Add(uiLabel1);
            uiGroupBox2.Controls.Add(uiButton6);
            uiGroupBox2.Controls.Add(uiComboBox1);
            uiGroupBox2.Controls.Add(uiLabel4);
            uiGroupBox2.Controls.Add(uiLight1);
            uiGroupBox2.Controls.Add(SwitchBaudrate);
            uiGroupBox2.Controls.Add(uiButton2);
            uiGroupBox2.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiGroupBox2.Location = new Point(3, 40);
            uiGroupBox2.Margin = new Padding(4, 5, 4, 5);
            uiGroupBox2.MinimumSize = new Size(1, 1);
            uiGroupBox2.Name = "uiGroupBox2";
            uiGroupBox2.Padding = new Padding(0, 32, 0, 0);
            uiGroupBox2.Size = new Size(318, 817);
            uiGroupBox2.TabIndex = 2;
            uiGroupBox2.Text = "设备连接";
            uiGroupBox2.TextAlignment = ContentAlignment.MiddleLeft;
            uiGroupBox2.Click += uiGroupBox2_Click;
            // 
            // uiSymbolLabel7
            // 
            uiSymbolLabel7.Anchor = AnchorStyles.Left;
            uiSymbolLabel7.Font = new Font("楷体", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiSymbolLabel7.Location = new Point(164, 63);
            uiSymbolLabel7.MinimumSize = new Size(1, 1);
            uiSymbolLabel7.Name = "uiSymbolLabel7";
            uiSymbolLabel7.Size = new Size(127, 35);
            uiSymbolLabel7.Symbol = 61466;
            uiSymbolLabel7.TabIndex = 44;
            uiSymbolLabel7.Text = "检查更新";
            uiSymbolLabel7.Click += uiSymbolLabel7_Click;
            // 
            // uiSymbolLabel1
            // 
            uiSymbolLabel1.Anchor = AnchorStyles.Left;
            uiSymbolLabel1.Font = new Font("楷体", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiSymbolLabel1.Location = new Point(127, 22);
            uiSymbolLabel1.MinimumSize = new Size(1, 1);
            uiSymbolLabel1.Name = "uiSymbolLabel1";
            uiSymbolLabel1.Size = new Size(188, 35);
            uiSymbolLabel1.Symbol = 62075;
            uiSymbolLabel1.TabIndex = 43;
            uiSymbolLabel1.Text = "版本信息";
            uiSymbolLabel1.TextAlign = ContentAlignment.MiddleLeft;
            uiSymbolLabel1.Click += uiSymbolLabel1_Click;
            // 
            // uiButton14
            // 
            uiButton14.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiButton14.Location = new Point(250, 756);
            uiButton14.MinimumSize = new Size(1, 1);
            uiButton14.Name = "uiButton14";
            uiButton14.Size = new Size(50, 26);
            uiButton14.TabIndex = 42;
            uiButton14.Text = "清空";
            uiButton14.TipsFont = new Font("宋体", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiButton14.Click += uiButton14_Click;
            // 
            // uiLabel19
            // 
            uiLabel19.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiLabel19.ForeColor = Color.FromArgb(48, 48, 48);
            uiLabel19.Location = new Point(16, 788);
            uiLabel19.Name = "uiLabel19";
            uiLabel19.Size = new Size(181, 23);
            uiLabel19.TabIndex = 41;
            uiLabel19.Text = "例：开CH1:5A A5 01 01";
            // 
            // rtb_Log
            // 
            rtb_Log.FillColor = Color.White;
            rtb_Log.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            rtb_Log.Location = new Point(17, 266);
            rtb_Log.Margin = new Padding(4, 5, 4, 5);
            rtb_Log.MinimumSize = new Size(1, 1);
            rtb_Log.Name = "rtb_Log";
            rtb_Log.Padding = new Padding(2);
            rtb_Log.ReadOnly = true;
            rtb_Log.ScrollBarBackColor = Color.Silver;
            rtb_Log.ScrollBarStyleInherited = false;
            rtb_Log.ScrollBarWidth = 6;
            rtb_Log.ShowText = false;
            rtb_Log.Size = new Size(283, 482);
            rtb_Log.TabIndex = 40;
            rtb_Log.TextAlignment = ContentAlignment.MiddleCenter;
            rtb_Log.WordWrap = false;
            rtb_Log.TextChanged += rtb_Log_TextChanged;
            // 
            // uiButton8
            // 
            uiButton8.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiButton8.Location = new Point(210, 788);
            uiButton8.MinimumSize = new Size(1, 1);
            uiButton8.Name = "uiButton8";
            uiButton8.Size = new Size(85, 23);
            uiButton8.TabIndex = 39;
            uiButton8.Text = "发送指令";
            uiButton8.TipsFont = new Font("宋体", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiButton8.Click += uiButton8_Click;
            // 
            // uiTextBox7
            // 
            uiTextBox7.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiTextBox7.Location = new Point(17, 754);
            uiTextBox7.Margin = new Padding(4, 5, 4, 5);
            uiTextBox7.MinimumSize = new Size(1, 16);
            uiTextBox7.Name = "uiTextBox7";
            uiTextBox7.Padding = new Padding(5);
            uiTextBox7.ShowText = false;
            uiTextBox7.Size = new Size(229, 29);
            uiTextBox7.TabIndex = 38;
            uiTextBox7.TextAlignment = ContentAlignment.MiddleLeft;
            uiTextBox7.Watermark = "";
            // 
            // uiLabel5
            // 
            uiLabel5.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiLabel5.ForeColor = Color.FromArgb(48, 48, 48);
            uiLabel5.Location = new Point(19, 75);
            uiLabel5.Name = "uiLabel5";
            uiLabel5.Size = new Size(100, 23);
            uiLabel5.TabIndex = 36;
            uiLabel5.Text = "驱动板串口";
            // 
            // uiLabel1
            // 
            uiLabel1.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiLabel1.ForeColor = Color.FromArgb(48, 48, 48);
            uiLabel1.Location = new Point(19, 222);
            uiLabel1.Name = "uiLabel1";
            uiLabel1.Size = new Size(100, 23);
            uiLabel1.TabIndex = 34;
            uiLabel1.Text = "串口测试log";
            // 
            // uiButton6
            // 
            uiButton6.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiButton6.Location = new Point(16, 35);
            uiButton6.MinimumSize = new Size(1, 1);
            uiButton6.Name = "uiButton6";
            uiButton6.Size = new Size(105, 30);
            uiButton6.TabIndex = 32;
            uiButton6.Text = "刷新串口列表";
            uiButton6.TipsFont = new Font("宋体", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiButton6.Click += uiButton6_Click_1;
            // 
            // uiComboBox1
            // 
            uiComboBox1.DataSource = null;
            uiComboBox1.FillColor = Color.White;
            uiComboBox1.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiComboBox1.ItemHoverColor = Color.FromArgb(155, 200, 255);
            uiComboBox1.ItemSelectForeColor = Color.FromArgb(235, 243, 255);
            uiComboBox1.Location = new Point(19, 98);
            uiComboBox1.Margin = new Padding(4, 5, 4, 5);
            uiComboBox1.MinimumSize = new Size(63, 0);
            uiComboBox1.Name = "uiComboBox1";
            uiComboBox1.Padding = new Padding(0, 0, 30, 2);
            uiComboBox1.Size = new Size(281, 29);
            uiComboBox1.SymbolSize = 24;
            uiComboBox1.TabIndex = 17;
            uiComboBox1.TextAlignment = ContentAlignment.MiddleLeft;
            uiComboBox1.Watermark = "";
            // 
            // uiLabel4
            // 
            uiLabel4.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiLabel4.ForeColor = Color.FromArgb(48, 48, 48);
            uiLabel4.Location = new Point(16, 141);
            uiLabel4.Margin = new Padding(0);
            uiLabel4.Name = "uiLabel4";
            uiLabel4.Size = new Size(63, 29);
            uiLabel4.TabIndex = 21;
            uiLabel4.Text = "波特率";
            uiLabel4.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // uiLight1
            // 
            uiLight1.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiLight1.Location = new Point(256, 173);
            uiLight1.MinimumSize = new Size(1, 1);
            uiLight1.Name = "uiLight1";
            uiLight1.OnColor = Color.Red;
            uiLight1.Radius = 35;
            uiLight1.Size = new Size(35, 35);
            uiLight1.TabIndex = 18;
            uiLight1.Text = "uiLight1";
            // 
            // SwitchBaudrate
            // 
            SwitchBaudrate.DataSource = null;
            SwitchBaudrate.FillColor = Color.White;
            SwitchBaudrate.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            SwitchBaudrate.ItemHoverColor = Color.FromArgb(155, 200, 255);
            SwitchBaudrate.Items.AddRange(new object[] { "115200", "57600", "38400" });
            SwitchBaudrate.ItemSelectForeColor = Color.FromArgb(235, 243, 255);
            SwitchBaudrate.Location = new Point(101, 141);
            SwitchBaudrate.Margin = new Padding(4, 5, 4, 5);
            SwitchBaudrate.MinimumSize = new Size(63, 0);
            SwitchBaudrate.Name = "SwitchBaudrate";
            SwitchBaudrate.Padding = new Padding(0, 0, 30, 2);
            SwitchBaudrate.Size = new Size(116, 29);
            SwitchBaudrate.SymbolSize = 24;
            SwitchBaudrate.TabIndex = 20;
            SwitchBaudrate.TextAlignment = ContentAlignment.MiddleLeft;
            SwitchBaudrate.Watermark = "";
            // 
            // uiButton2
            // 
            uiButton2.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiButton2.Location = new Point(16, 173);
            uiButton2.MinimumSize = new Size(1, 1);
            uiButton2.Name = "uiButton2";
            uiButton2.Size = new Size(76, 30);
            uiButton2.TabIndex = 19;
            uiButton2.Text = "打开";
            uiButton2.TipsFont = new Font("宋体", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            uiButton2.Click += uiButton2_Click_1;
            // 
            // Form1
            // 
            AutoScaleMode = AutoScaleMode.None;
            AutoSize = true;
            ClientSize = new Size(1545, 869);
            Controls.Add(uiGroupBox2);
            Controls.Add(uiGroupBox1);
            Name = "Form1";
            RectColor = Color.White;
            TitleColor = Color.DeepSkyBlue;
            ZoomScaleRect = new Rectangle(15, 15, 1552, 825);
            uiGroupBox1.ResumeLayout(false);
            uiPanel3.ResumeLayout(false);
            uiFlowLayoutPanel3.ResumeLayout(false);
            uiCheckBoxGroup1.ResumeLayout(false);
            uiTableLayoutPanel7.ResumeLayout(false);
            uiGroupBox2.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private System.ComponentModel.BackgroundWorker backgroundWorker1;
        private Sunny.UI.UIGroupBox uiGroupBox1;
        private Sunny.UI.UIGroupBox uiGroupBox2;
        private Sunny.UI.UIButton uiButton6;
        private Sunny.UI.UIComboBox uiComboBox1;
        private Sunny.UI.UILabel uiLabel4;
        private Sunny.UI.UILight uiLight1;
        private Sunny.UI.UIComboBox SwitchBaudrate;
        private Sunny.UI.UIButton uiButton2;
        private Sunny.UI.UILabel uiLabel1;
        private Sunny.UI.UIButton connectButton;
        private UILabel uiLabel5;
        private UIPanel uiPanel3;
        private UIFlowLayoutPanel uiFlowLayoutPanel3;
        private UICheckBoxGroup uiCheckBoxGroup1;
        private UILabel uiLabel11;
        private UIComboBox Omtransport;
        private UIButton uiButton5;
        private UILabel uiLabel14;
        private UILabel uiLabel13;
        private UIComboBox Ommodel;
        private UILabel uiLabel12;
        private UIComboBox Omprotocol;
        private UIButton uiButton7;
        private UIButton uiButton8;
        private UITextBox uiTextBox7;
        private UIRichTextBox rtb_Log;
        private UILabel uiLabel19;
        private UILabel uiLabel20;
        private UITextBox uiTextBox1;
        private UIComboBox uiComboBox2;
        private UILabel uiLabel24;
        private UIButton uiButton14;
        private UISymbolLabel uiSymbolLabel1;
        private UISymbolLabel uiSymbolLabel7;
        private UILabel uiLabel6;
        private UIComboBox baudrate;
        private UILabel uiLabel3;
        private UIComboBox switchCom;
        private UITableLayoutPanel uiTableLayoutPanel6;
        private UITableLayoutPanel uiTableLayoutPanel5;
        private UITableLayoutPanel uiTableLayoutPanel4;
        private UITableLayoutPanel uiTableLayoutPanel3;
        private UITableLayoutPanel uiTableLayoutPanel2;
        private UITableLayoutPanel uiTableLayoutPanel1;
        private UIButton checkAll;
        private UILabel uiLabel2;
        private UITextBox waveLenText;
        private UILabel uiLabel7;
        private UITableLayoutPanel uiTableLayoutPanel7;
        private UITextBox checkList;
        private UILabel uiLabel8;
        private UILabel uiLabel10;
    }
}
