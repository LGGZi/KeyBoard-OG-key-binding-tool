namespace KeyboardOG
{
    partial class Form1
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.panelTop = new System.Windows.Forms.Panel();
            this.lblConnBadge = new System.Windows.Forms.Label();
            this.lblStatus = new System.Windows.Forms.Label();
            this.lblFirmware = new System.Windows.Forms.Label();
            this.lblQueue = new System.Windows.Forms.Label();
            this.lblConnState = new System.Windows.Forms.Label();
            this.btnSettings = new System.Windows.Forms.Button();
            this.btnOpenLog = new System.Windows.Forms.Button();
            this.btnConnect = new System.Windows.Forms.Button();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.cboDevices = new System.Windows.Forms.ComboBox();
            this.labelDevice = new System.Windows.Forms.Label();
            this.lblActionHint = new System.Windows.Forms.Label();
            this.panelActions = new System.Windows.Forms.Panel();
            this.btnClearAll = new System.Windows.Forms.Button();
            this.lblDangerTitle = new System.Windows.Forms.Label();
            this.sepB = new System.Windows.Forms.Panel();
            this.lblGlobalTitle = new System.Windows.Forms.Label();
            this.sepA = new System.Windows.Forms.Panel();
            this.btnWriteAll = new System.Windows.Forms.Button();
            this.btnWriteKey = new System.Windows.Forms.Button();
            this.lblSingleTitle = new System.Windows.Forms.Label();
            this.panelMain = new System.Windows.Forms.Panel();
            this.panelRight = new System.Windows.Forms.Panel();
            this.panelPickerHost = new System.Windows.Forms.Panel();
            this.tabsPicker = new System.Windows.Forms.TabControl();
            this.tabKeyboard = new System.Windows.Forms.TabPage();
            this.panelKbScroll = new System.Windows.Forms.Panel();
            this.keyboard1 = new KeyboardOG.Keyboard();
            this.tabMouse = new System.Windows.Forms.TabPage();
            this.mousePicker = new KeyboardOG.MouseControl();
            this.tabGamepad = new System.Windows.Forms.TabPage();
            this.gamepadPicker = new KeyboardOG.GamepadControl();
            this.tabConsumer = new System.Windows.Forms.TabPage();
            this.consumerPicker = new KeyboardOG.ConsumerControl();
            this.lblPickerHint = new System.Windows.Forms.Label();
            this.hallPanel = new KeyboardOG.HallParamControl();
            this.panelLeft = new System.Windows.Forms.Panel();
            this.grpSlots = new System.Windows.Forms.GroupBox();
            this.panelSync = new System.Windows.Forms.Panel();
            this.btnSyncMaps = new System.Windows.Forms.Button();
            this.panelBindBtns = new System.Windows.Forms.Panel();
            this.btnSlotUp = new System.Windows.Forms.Button();
            this.btnSlotDown = new System.Windows.Forms.Button();
            this.btnSlotDelete = new System.Windows.Forms.Button();
            this.btnSlotClear = new System.Windows.Forms.Button();
            this.lvBindings = new System.Windows.Forms.ListView();
            this.colSlotIndex = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colSlotText = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.panelSlotTitle = new System.Windows.Forms.Panel();
            this.lblSlotTitle = new System.Windows.Forms.Label();
            this.btnClearAllLocal = new System.Windows.Forms.Button();
            this.grpKey = new System.Windows.Forms.GroupBox();
            this.lblKeyHint = new System.Windows.Forms.Label();
            this.lblKeyReserved = new System.Windows.Forms.Label();
            this.flpKeys = new System.Windows.Forms.FlowLayoutPanel();
            this.btnSyncParams = new System.Windows.Forms.Button();
            this.panelTop.SuspendLayout();
            this.panelActions.SuspendLayout();
            this.panelMain.SuspendLayout();
            this.panelRight.SuspendLayout();
            this.panelPickerHost.SuspendLayout();
            this.tabsPicker.SuspendLayout();
            this.tabKeyboard.SuspendLayout();
            this.panelKbScroll.SuspendLayout();
            this.tabMouse.SuspendLayout();
            this.tabGamepad.SuspendLayout();
            this.tabConsumer.SuspendLayout();
            this.panelLeft.SuspendLayout();
            this.grpSlots.SuspendLayout();
            this.panelSync.SuspendLayout();
            this.panelBindBtns.SuspendLayout();
            this.panelSlotTitle.SuspendLayout();
            this.grpKey.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelTop
            // 
            this.panelTop.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(244)))), ((int)(((byte)(249)))));
            this.panelTop.Controls.Add(this.lblConnBadge);
            this.panelTop.Controls.Add(this.lblStatus);
            this.panelTop.Controls.Add(this.lblFirmware);
            this.panelTop.Controls.Add(this.lblQueue);
            this.panelTop.Controls.Add(this.lblConnState);
            this.panelTop.Controls.Add(this.btnSettings);
            this.panelTop.Controls.Add(this.btnOpenLog);
            this.panelTop.Controls.Add(this.btnConnect);
            this.panelTop.Controls.Add(this.btnRefresh);
            this.panelTop.Controls.Add(this.cboDevices);
            this.panelTop.Controls.Add(this.labelDevice);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Location = new System.Drawing.Point(0, 0);
            this.panelTop.Name = "panelTop";
            this.panelTop.Size = new System.Drawing.Size(1700, 104);
            this.panelTop.TabIndex = 0;
            // 
            // lblConnBadge
            // 
            this.lblConnBadge.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this.lblConnBadge.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblConnBadge.ForeColor = System.Drawing.Color.White;
            this.lblConnBadge.Location = new System.Drawing.Point(866, 11);
            this.lblConnBadge.Name = "lblConnBadge";
            this.lblConnBadge.Size = new System.Drawing.Size(180, 28);
            this.lblConnBadge.TabIndex = 12;
            this.lblConnBadge.Text = "● 设备未连接";
            this.lblConnBadge.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblStatus
            // 
            this.lblStatus.AutoEllipsis = true;
            this.lblStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(70)))), ((int)(((byte)(150)))));
            this.lblStatus.Location = new System.Drawing.Point(12, 74);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(1028, 20);
            this.lblStatus.TabIndex = 10;
            this.lblStatus.Text = "就绪。请先点“刷新设备”，选中你的键盘后点“连接”。";
            this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblFirmware
            // 
            this.lblFirmware.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblFirmware.Location = new System.Drawing.Point(1266, 11);
            this.lblFirmware.Name = "lblFirmware";
            this.lblFirmware.Size = new System.Drawing.Size(202, 90);
            this.lblFirmware.TabIndex = 13;
            this.lblFirmware.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblQueue
            // 
            this.lblQueue.AutoSize = true;
            this.lblQueue.ForeColor = System.Drawing.Color.DimGray;
            this.lblQueue.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblQueue.Location = new System.Drawing.Point(1472, 50);
            this.lblQueue.Name = "lblQueue";
            this.lblQueue.Size = new System.Drawing.Size(51, 17);
            this.lblQueue.TabIndex = 9;
            this.lblQueue.Text = "队列：0";
            // 
            // lblConnState
            // 
            this.lblConnState.AutoEllipsis = true;
            this.lblConnState.ForeColor = System.Drawing.Color.DimGray;
            this.lblConnState.Location = new System.Drawing.Point(12, 46);
            this.lblConnState.Name = "lblConnState";
            this.lblConnState.Size = new System.Drawing.Size(1230, 22);
            this.lblConnState.TabIndex = 5;
            this.lblConnState.Text = "未连接";
            this.lblConnState.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // btnSettings
            // 
            this.btnSettings.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSettings.Location = new System.Drawing.Point(1476, 11);
            this.btnSettings.Name = "btnSettings";
            this.btnSettings.Size = new System.Drawing.Size(96, 28);
            this.btnSettings.TabIndex = 11;
            this.btnSettings.Text = "设置/config";
            this.btnSettings.UseVisualStyleBackColor = true;
            // 
            // btnOpenLog
            // 
            this.btnOpenLog.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOpenLog.Location = new System.Drawing.Point(1572, 11);
            this.btnOpenLog.Name = "btnOpenLog";
            this.btnOpenLog.Size = new System.Drawing.Size(116, 28);
            this.btnOpenLog.TabIndex = 4;
            this.btnOpenLog.Text = "指令日志…";
            this.btnOpenLog.UseVisualStyleBackColor = true;
            // 
            // btnConnect
            // 
            this.btnConnect.Location = new System.Drawing.Point(766, 11);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(92, 28);
            this.btnConnect.TabIndex = 3;
            this.btnConnect.Text = "连接";
            this.btnConnect.UseVisualStyleBackColor = true;
            // 
            // btnRefresh
            // 
            this.btnRefresh.Location = new System.Drawing.Point(668, 11);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(92, 28);
            this.btnRefresh.TabIndex = 2;
            this.btnRefresh.Text = "刷新设备";
            this.btnRefresh.UseVisualStyleBackColor = true;
            // 
            // cboDevices
            // 
            this.cboDevices.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboDevices.FormattingEnabled = true;
            this.cboDevices.IntegralHeight = false;
            this.cboDevices.Location = new System.Drawing.Point(58, 12);
            this.cboDevices.Name = "cboDevices";
            this.cboDevices.Size = new System.Drawing.Size(600, 25);
            this.cboDevices.TabIndex = 1;
            // 
            // labelDevice
            // 
            this.labelDevice.AutoSize = true;
            this.labelDevice.Location = new System.Drawing.Point(12, 16);
            this.labelDevice.Name = "labelDevice";
            this.labelDevice.Size = new System.Drawing.Size(44, 17);
            this.labelDevice.TabIndex = 0;
            this.labelDevice.Text = "设备：";
            // 
            // lblActionHint
            // 
            this.lblActionHint.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(110)))), ((int)(((byte)(110)))), ((int)(((byte)(115)))));
            this.lblActionHint.Location = new System.Drawing.Point(196, 34);
            this.lblActionHint.Name = "lblActionHint";
            this.lblActionHint.Size = new System.Drawing.Size(640, 40);
            this.lblActionHint.TabIndex = 9;
            this.lblActionHint.Text = "主操作 = 把左侧列表整片写进当前按键，只动这一个键；后面的空槽由键盘自动清零，\r\n所以只发本地有的那几条映射。写完自动发 CMD_REFRESH，不用重新插拔键" +
    "盘。";
            // 
            // panelActions
            // 
            this.panelActions.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(248)))), ((int)(((byte)(250)))));
            this.panelActions.Controls.Add(this.btnClearAll);
            this.panelActions.Controls.Add(this.lblDangerTitle);
            this.panelActions.Controls.Add(this.sepB);
            this.panelActions.Controls.Add(this.lblGlobalTitle);
            this.panelActions.Controls.Add(this.sepA);
            this.panelActions.Controls.Add(this.lblActionHint);
            this.panelActions.Controls.Add(this.btnWriteAll);
            this.panelActions.Controls.Add(this.btnWriteKey);
            this.panelActions.Controls.Add(this.lblSingleTitle);
            this.panelActions.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelActions.Location = new System.Drawing.Point(0, 808);
            this.panelActions.Name = "panelActions";
            this.panelActions.Size = new System.Drawing.Size(1700, 92);
            this.panelActions.TabIndex = 1;
            // 
            // btnClearAll
            // 
            this.btnClearAll.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(238)))), ((int)(((byte)(238)))));
            this.btnClearAll.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(190)))), ((int)(((byte)(70)))), ((int)(((byte)(70)))));
            this.btnClearAll.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnClearAll.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(160)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.btnClearAll.Location = new System.Drawing.Point(12, 30);
            this.btnClearAll.Name = "btnClearAll";
            this.btnClearAll.Size = new System.Drawing.Size(148, 36);
            this.btnClearAll.TabIndex = 8;
            this.btnClearAll.Text = "清空全部映射 (CLEAR)";
            this.btnClearAll.UseVisualStyleBackColor = false;
            // 
            // lblDangerTitle
            // 
            this.lblDangerTitle.AutoSize = true;
            this.lblDangerTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(170)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this.lblDangerTitle.Location = new System.Drawing.Point(12, 8);
            this.lblDangerTitle.Name = "lblDangerTitle";
            this.lblDangerTitle.Size = new System.Drawing.Size(92, 17);
            this.lblDangerTitle.TabIndex = 7;
            this.lblDangerTitle.Text = "③ ⚠ 危险操作";
            // 
            // sepB
            // 
            this.sepB.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(160)))), ((int)(((byte)(160)))));
            this.sepB.Location = new System.Drawing.Point(180, 12);
            this.sepB.Name = "sepB";
            this.sepB.Size = new System.Drawing.Size(1, 72);
            this.sepB.TabIndex = 6;
            // 
            // lblGlobalTitle
            // 
            this.lblGlobalTitle.AutoSize = true;
            this.lblGlobalTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(90)))), ((int)(((byte)(160)))));
            this.lblGlobalTitle.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblGlobalTitle.Location = new System.Drawing.Point(1076, 8);
            this.lblGlobalTitle.Name = "lblGlobalTitle";
            this.lblGlobalTitle.Size = new System.Drawing.Size(96, 17);
            this.lblGlobalTitle.TabIndex = 4;
            this.lblGlobalTitle.Text = "② 全部映射操作";
            // 
            // sepA
            // 
            this.sepA.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(210)))), ((int)(((byte)(210)))), ((int)(((byte)(210)))));
            this.sepA.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.sepA.Location = new System.Drawing.Point(1328, 12);
            this.sepA.Name = "sepA";
            this.sepA.Size = new System.Drawing.Size(1, 72);
            this.sepA.TabIndex = 3;
            // 
            // btnWriteAll
            // 
            this.btnWriteAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnWriteAll.Location = new System.Drawing.Point(1076, 30);
            this.btnWriteAll.Name = "btnWriteAll";
            this.btnWriteAll.Size = new System.Drawing.Size(232, 44);
            this.btnWriteAll.TabIndex = 2;
            this.btnWriteAll.Text = "写入全部按键映射（0 ~ 13）";
            this.btnWriteAll.UseVisualStyleBackColor = true;
            // 
            // btnWriteKey
            // 
            this.btnWriteKey.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(210)))), ((int)(((byte)(232)))), ((int)(((byte)(255)))));
            this.btnWriteKey.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(120)))), ((int)(((byte)(190)))));
            this.btnWriteKey.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnWriteKey.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Bold);
            this.btnWriteKey.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(15)))), ((int)(((byte)(60)))), ((int)(((byte)(130)))));
            this.btnWriteKey.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnWriteKey.Location = new System.Drawing.Point(1348, 28);
            this.btnWriteKey.Name = "btnWriteKey";
            this.btnWriteKey.Size = new System.Drawing.Size(340, 44);
            this.btnWriteKey.TabIndex = 1;
            this.btnWriteKey.Text = "写入当前按键映射（全部 12 个槽）";
            this.btnWriteKey.UseVisualStyleBackColor = false;
            // 
            // lblSingleTitle
            // 
            this.lblSingleTitle.AutoSize = true;
            this.lblSingleTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F, System.Drawing.FontStyle.Bold);
            this.lblSingleTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(80)))), ((int)(((byte)(160)))));
            this.lblSingleTitle.Location = new System.Drawing.Point(1348, 4);
            this.lblSingleTitle.Name = "lblSingleTitle";
            this.lblSingleTitle.Size = new System.Drawing.Size(195, 19);
            this.lblSingleTitle.TabIndex = 0;
            this.lblSingleTitle.Text = "① 写入当前按键映射（主操作）";
            // 
            // panelMain
            // 
            this.panelMain.Controls.Add(this.panelRight);
            this.panelMain.Controls.Add(this.panelLeft);
            this.panelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelMain.Location = new System.Drawing.Point(0, 104);
            this.panelMain.Name = "panelMain";
            this.panelMain.Size = new System.Drawing.Size(1700, 704);
            this.panelMain.TabIndex = 2;
            // 
            // panelRight
            // 
            this.panelRight.Controls.Add(this.panelPickerHost);
            this.panelRight.Controls.Add(this.hallPanel);
            this.panelRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelRight.Location = new System.Drawing.Point(364, 0);
            this.panelRight.Name = "panelRight";
            this.panelRight.Size = new System.Drawing.Size(1336, 704);
            this.panelRight.TabIndex = 1;
            // 
            // panelPickerHost
            // 
            this.panelPickerHost.Controls.Add(this.tabsPicker);
            this.panelPickerHost.Controls.Add(this.lblPickerHint);
            this.panelPickerHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelPickerHost.Location = new System.Drawing.Point(0, 0);
            this.panelPickerHost.Name = "panelPickerHost";
            this.panelPickerHost.Size = new System.Drawing.Size(1336, 498);
            this.panelPickerHost.TabIndex = 0;
            // 
            // tabsPicker
            // 
            this.tabsPicker.Controls.Add(this.tabKeyboard);
            this.tabsPicker.Controls.Add(this.tabMouse);
            this.tabsPicker.Controls.Add(this.tabGamepad);
            this.tabsPicker.Controls.Add(this.tabConsumer);
            this.tabsPicker.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabsPicker.Location = new System.Drawing.Point(0, 0);
            this.tabsPicker.Name = "tabsPicker";
            this.tabsPicker.SelectedIndex = 0;
            this.tabsPicker.Size = new System.Drawing.Size(1336, 472);
            this.tabsPicker.TabIndex = 0;
            // 
            // tabKeyboard
            // 
            this.tabKeyboard.Controls.Add(this.panelKbScroll);
            this.tabKeyboard.Location = new System.Drawing.Point(4, 26);
            this.tabKeyboard.Name = "tabKeyboard";
            this.tabKeyboard.Padding = new System.Windows.Forms.Padding(3);
            this.tabKeyboard.Size = new System.Drawing.Size(1108, 442);
            this.tabKeyboard.TabIndex = 0;
            this.tabKeyboard.Text = "键盘";
            this.tabKeyboard.UseVisualStyleBackColor = true;
            // 
            // panelKbScroll
            // 
            this.panelKbScroll.AutoScroll = true;
            this.panelKbScroll.Controls.Add(this.keyboard1);
            this.panelKbScroll.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelKbScroll.Location = new System.Drawing.Point(3, 3);
            this.panelKbScroll.Name = "panelKbScroll";
            this.panelKbScroll.Size = new System.Drawing.Size(1322, 436);
            this.panelKbScroll.TabIndex = 0;
            // 
            // keyboard1
            // 
            this.keyboard1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(245)))), ((int)(((byte)(245)))));
            this.keyboard1.Location = new System.Drawing.Point(14, 44);
            this.keyboard1.Name = "keyboard1";
            this.keyboard1.SelectionMode = true;
            this.keyboard1.Size = new System.Drawing.Size(1070, 286);
            this.keyboard1.TabIndex = 0;
            // 
            // tabMouse
            // 
            this.tabMouse.Controls.Add(this.mousePicker);
            this.tabMouse.Location = new System.Drawing.Point(4, 26);
            this.tabMouse.Name = "tabMouse";
            this.tabMouse.Padding = new System.Windows.Forms.Padding(3);
            this.tabMouse.Size = new System.Drawing.Size(1108, 442);
            this.tabMouse.TabIndex = 1;
            this.tabMouse.Text = "鼠标";
            this.tabMouse.UseVisualStyleBackColor = true;
            // 
            // mousePicker
            // 
            this.mousePicker.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(245)))), ((int)(((byte)(245)))));
            this.mousePicker.Location = new System.Drawing.Point(392, 6);
            this.mousePicker.Name = "mousePicker";
            this.mousePicker.SelectionMode = true;
            this.mousePicker.Size = new System.Drawing.Size(320, 430);
            this.mousePicker.TabIndex = 0;
            // 
            // tabGamepad
            // 
            this.tabGamepad.Controls.Add(this.gamepadPicker);
            this.tabGamepad.Location = new System.Drawing.Point(4, 26);
            this.tabGamepad.Name = "tabGamepad";
            this.tabGamepad.Padding = new System.Windows.Forms.Padding(3);
            this.tabGamepad.Size = new System.Drawing.Size(1108, 442);
            this.tabGamepad.TabIndex = 2;
            this.tabGamepad.Text = "手柄";
            this.tabGamepad.UseVisualStyleBackColor = true;
            // 
            // gamepadPicker
            // 
            this.gamepadPicker.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(245)))), ((int)(((byte)(245)))));
            this.gamepadPicker.Location = new System.Drawing.Point(262, 12);
            this.gamepadPicker.Name = "gamepadPicker";
            this.gamepadPicker.SelectionMode = true;
            this.gamepadPicker.Size = new System.Drawing.Size(580, 420);
            this.gamepadPicker.TabIndex = 0;
            // 
            // tabConsumer
            // 
            this.tabConsumer.Controls.Add(this.consumerPicker);
            this.tabConsumer.Location = new System.Drawing.Point(4, 26);
            this.tabConsumer.Name = "tabConsumer";
            this.tabConsumer.Padding = new System.Windows.Forms.Padding(3);
            this.tabConsumer.Size = new System.Drawing.Size(1108, 442);
            this.tabConsumer.TabIndex = 3;
            this.tabConsumer.Text = "多媒体";
            this.tabConsumer.UseVisualStyleBackColor = true;
            // 
            // consumerPicker
            // 
            this.consumerPicker.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(245)))), ((int)(((byte)(245)))));
            this.consumerPicker.Location = new System.Drawing.Point(292, 70);
            this.consumerPicker.Name = "consumerPicker";
            this.consumerPicker.SelectionMode = true;
            this.consumerPicker.Size = new System.Drawing.Size(520, 300);
            this.consumerPicker.TabIndex = 0;
            // 
            // lblPickerHint
            // 
            this.lblPickerHint.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.lblPickerHint.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.lblPickerHint.Location = new System.Drawing.Point(0, 472);
            this.lblPickerHint.Name = "lblPickerHint";
            this.lblPickerHint.Size = new System.Drawing.Size(1116, 26);
            this.lblPickerHint.TabIndex = 1;
            this.lblPickerHint.Text = "当前按键 #0 · 已选 0/12 · 在上方页面里点击要映射到的按键";
            this.lblPickerHint.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // hallPanel
            // 
            this.hallPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(245)))), ((int)(((byte)(245)))));
            this.hallPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.hallPanel.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.hallPanel.Location = new System.Drawing.Point(0, 498);
            this.hallPanel.Name = "hallPanel";
            this.hallPanel.SelectedKey = 0;
            this.hallPanel.Size = new System.Drawing.Size(1336, 206);
            this.hallPanel.TabIndex = 1;
            // 
            // panelLeft
            // 
            this.panelLeft.Controls.Add(this.grpSlots);
            this.panelLeft.Controls.Add(this.grpKey);
            this.panelLeft.Dock = System.Windows.Forms.DockStyle.Left;
            this.panelLeft.Location = new System.Drawing.Point(0, 0);
            this.panelLeft.Name = "panelLeft";
            this.panelLeft.Padding = new System.Windows.Forms.Padding(4, 0, 4, 4);
            this.panelLeft.Size = new System.Drawing.Size(364, 704);
            this.panelLeft.TabIndex = 0;
            // 
            // grpSlots
            // 
            this.grpSlots.Controls.Add(this.panelSync);
            this.grpSlots.Controls.Add(this.panelBindBtns);
            this.grpSlots.Controls.Add(this.lvBindings);
            this.grpSlots.Controls.Add(this.panelSlotTitle);
            this.grpSlots.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpSlots.Location = new System.Drawing.Point(4, 152);
            this.grpSlots.Name = "grpSlots";
            this.grpSlots.Size = new System.Drawing.Size(356, 548);
            this.grpSlots.TabIndex = 1;
            this.grpSlots.TabStop = false;
            this.grpSlots.Text = "2. 按键映射列表";
            // 
            // panelSync
            // 
            this.panelSync.Controls.Add(this.btnSyncMaps);
            this.panelSync.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelSync.Location = new System.Drawing.Point(3, 511);
            this.panelSync.Name = "panelSync";
            this.panelSync.Size = new System.Drawing.Size(350, 34);
            this.panelSync.TabIndex = 3;
            // 
            // btnSyncMaps
            // 
            this.btnSyncMaps.Location = new System.Drawing.Point(0, 2);
            this.btnSyncMaps.Name = "btnSyncMaps";
            this.btnSyncMaps.Size = new System.Drawing.Size(258, 28);
            this.btnSyncMaps.TabIndex = 0;
            this.btnSyncMaps.Text = "重新读取映射（放弃改动）";
            this.btnSyncMaps.UseVisualStyleBackColor = true;
            // 
            // panelBindBtns
            // 
            this.panelBindBtns.Controls.Add(this.btnSlotUp);
            this.panelBindBtns.Controls.Add(this.btnSlotDown);
            this.panelBindBtns.Controls.Add(this.btnSlotDelete);
            this.panelBindBtns.Controls.Add(this.btnSlotClear);
            this.panelBindBtns.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelBindBtns.Location = new System.Drawing.Point(3, 477);
            this.panelBindBtns.Name = "panelBindBtns";
            this.panelBindBtns.Size = new System.Drawing.Size(350, 32);
            this.panelBindBtns.TabIndex = 2;
            // 
            // btnSlotUp
            // 
            this.btnSlotUp.Location = new System.Drawing.Point(0, 3);
            this.btnSlotUp.Name = "btnSlotUp";
            this.btnSlotUp.Size = new System.Drawing.Size(56, 26);
            this.btnSlotUp.TabIndex = 0;
            this.btnSlotUp.Text = "↑ 上移";
            this.btnSlotUp.UseVisualStyleBackColor = true;
            // 
            // btnSlotDown
            // 
            this.btnSlotDown.Location = new System.Drawing.Point(62, 3);
            this.btnSlotDown.Name = "btnSlotDown";
            this.btnSlotDown.Size = new System.Drawing.Size(66, 26);
            this.btnSlotDown.TabIndex = 1;
            this.btnSlotDown.Text = "↓ 下移";
            this.btnSlotDown.UseVisualStyleBackColor = true;
            // 
            // btnSlotDelete
            // 
            this.btnSlotDelete.Location = new System.Drawing.Point(132, 3);
            this.btnSlotDelete.Name = "btnSlotDelete";
            this.btnSlotDelete.Size = new System.Drawing.Size(56, 26);
            this.btnSlotDelete.TabIndex = 2;
            this.btnSlotDelete.Text = "删除";
            this.btnSlotDelete.UseVisualStyleBackColor = true;
            // 
            // btnSlotClear
            // 
            this.btnSlotClear.Location = new System.Drawing.Point(192, 3);
            this.btnSlotClear.Name = "btnSlotClear";
            this.btnSlotClear.Size = new System.Drawing.Size(138, 26);
            this.btnSlotClear.TabIndex = 3;
            this.btnSlotClear.Text = "清空该键";
            this.btnSlotClear.UseVisualStyleBackColor = true;
            // 
            // lvBindings
            // 
            this.lvBindings.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colSlotIndex,
            this.colSlotText});
            this.lvBindings.Dock = System.Windows.Forms.DockStyle.Top;
            this.lvBindings.FullRowSelect = true;
            this.lvBindings.GridLines = true;
            this.lvBindings.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.lvBindings.HideSelection = false;
            this.lvBindings.Location = new System.Drawing.Point(3, 47);
            this.lvBindings.MultiSelect = false;
            this.lvBindings.Name = "lvBindings";
            this.lvBindings.Size = new System.Drawing.Size(350, 430);
            this.lvBindings.TabIndex = 1;
            this.lvBindings.UseCompatibleStateImageBehavior = false;
            this.lvBindings.View = System.Windows.Forms.View.Details;
            // 
            // colSlotIndex
            // 
            this.colSlotIndex.Text = "#";
            this.colSlotIndex.Width = 34;
            // 
            // colSlotText
            // 
            this.colSlotText.Text = "映射内容";
            this.colSlotText.Width = 300;
            // 
            // panelSlotTitle
            // 
            this.panelSlotTitle.Controls.Add(this.lblSlotTitle);
            this.panelSlotTitle.Controls.Add(this.btnClearAllLocal);
            this.panelSlotTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelSlotTitle.Location = new System.Drawing.Point(3, 19);
            this.panelSlotTitle.Name = "panelSlotTitle";
            this.panelSlotTitle.Size = new System.Drawing.Size(350, 28);
            this.panelSlotTitle.TabIndex = 0;
            // 
            // lblSlotTitle
            // 
            this.lblSlotTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSlotTitle.Location = new System.Drawing.Point(0, 0);
            this.lblSlotTitle.Name = "lblSlotTitle";
            this.lblSlotTitle.Size = new System.Drawing.Size(196, 28);
            this.lblSlotTitle.TabIndex = 0;
            this.lblSlotTitle.Text = "按键 #0：0/12 条";
            this.lblSlotTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // btnClearAllLocal
            // 
            this.btnClearAllLocal.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(238)))), ((int)(((byte)(238)))));
            this.btnClearAllLocal.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnClearAllLocal.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(190)))), ((int)(((byte)(70)))), ((int)(((byte)(70)))));
            this.btnClearAllLocal.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnClearAllLocal.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(170)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.btnClearAllLocal.Location = new System.Drawing.Point(196, 0);
            this.btnClearAllLocal.Name = "btnClearAllLocal";
            this.btnClearAllLocal.Size = new System.Drawing.Size(154, 28);
            this.btnClearAllLocal.TabIndex = 1;
            this.btnClearAllLocal.Text = "清空全部按键映射";
            this.btnClearAllLocal.UseVisualStyleBackColor = false;
            // 
            // grpKey
            // 
            this.grpKey.Controls.Add(this.lblKeyHint);
            this.grpKey.Controls.Add(this.lblKeyReserved);
            this.grpKey.Controls.Add(this.flpKeys);
            this.grpKey.Dock = System.Windows.Forms.DockStyle.Top;
            this.grpKey.Location = new System.Drawing.Point(4, 0);
            this.grpKey.Name = "grpKey";
            this.grpKey.Size = new System.Drawing.Size(356, 152);
            this.grpKey.TabIndex = 0;
            this.grpKey.TabStop = false;
            this.grpKey.Text = "1. 选择要修改的按键";
            // 
            // lblKeyHint
            // 
            this.lblKeyHint.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(110)))), ((int)(((byte)(110)))), ((int)(((byte)(115)))));
            this.lblKeyHint.Location = new System.Drawing.Point(8, 104);
            this.lblKeyHint.Name = "lblKeyHint";
            this.lblKeyHint.Size = new System.Drawing.Size(342, 20);
            this.lblKeyHint.TabIndex = 2;
            this.lblKeyHint.Text = "蓝底 = 选中　绿字 = 与键盘一致　红字 = 本地有改动（参数面板同一套）";
            // 
            // lblKeyReserved
            // 
            this.lblKeyReserved.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(175)))), ((int)(((byte)(175)))), ((int)(((byte)(180)))));
            this.lblKeyReserved.Location = new System.Drawing.Point(8, 126);
            this.lblKeyReserved.Name = "lblKeyReserved";
            this.lblKeyReserved.Size = new System.Drawing.Size(342, 20);
            this.lblKeyReserved.TabIndex = 1;
            this.lblKeyReserved.Text = "（下方预留：后续可换成其他样式的按键选择器）";
            this.lblKeyReserved.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // flpKeys
            // 
            this.flpKeys.Location = new System.Drawing.Point(8, 22);
            this.flpKeys.Name = "flpKeys";
            this.flpKeys.Size = new System.Drawing.Size(342, 84);
            this.flpKeys.TabIndex = 0;
            // 
            // btnSyncParams
            // 
            this.btnSyncParams.Location = new System.Drawing.Point(178, 2);
            this.btnSyncParams.Name = "btnSyncParams";
            this.btnSyncParams.Size = new System.Drawing.Size(172, 28);
            this.btnSyncParams.TabIndex = 1;
            this.btnSyncParams.Text = "重新读取磁轴参数";
            this.btnSyncParams.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(1700, 900);
            this.Controls.Add(this.panelMain);
            this.Controls.Add(this.panelActions);
            this.Controls.Add(this.panelTop);
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.MinimumSize = new System.Drawing.Size(1500, 820);   //再窄的话磁轴参数四块会挤到一起
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "KeyboardOG 改键 / 改参数工具";
            this.panelTop.ResumeLayout(false);
            this.panelTop.PerformLayout();
            this.panelActions.ResumeLayout(false);
            this.panelActions.PerformLayout();
            this.panelMain.ResumeLayout(false);
            this.panelRight.ResumeLayout(false);
            this.panelPickerHost.ResumeLayout(false);
            this.tabsPicker.ResumeLayout(false);
            this.tabKeyboard.ResumeLayout(false);
            this.panelKbScroll.ResumeLayout(false);
            this.tabMouse.ResumeLayout(false);
            this.tabGamepad.ResumeLayout(false);
            this.tabConsumer.ResumeLayout(false);
            this.panelLeft.ResumeLayout(false);
            this.grpSlots.ResumeLayout(false);
            this.panelSync.ResumeLayout(false);
            this.panelBindBtns.ResumeLayout(false);
            this.panelSlotTitle.ResumeLayout(false);
            this.grpKey.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Label labelDevice;
        private System.Windows.Forms.ComboBox cboDevices;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.Button btnOpenLog;
        private System.Windows.Forms.Button btnSettings;
        private System.Windows.Forms.Label lblConnState;



        private System.Windows.Forms.Label lblQueue;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Label lblFirmware;
        private System.Windows.Forms.Label lblActionHint;
        private System.Windows.Forms.Panel panelActions;
        private System.Windows.Forms.Label lblSingleTitle;
        private System.Windows.Forms.Button btnWriteKey;
        private System.Windows.Forms.Button btnWriteAll;
        private System.Windows.Forms.Panel sepA;
        private System.Windows.Forms.Label lblGlobalTitle;

        private System.Windows.Forms.Panel sepB;
        private System.Windows.Forms.Label lblDangerTitle;
        private System.Windows.Forms.Button btnClearAll;
        private System.Windows.Forms.Panel panelMain;
        private System.Windows.Forms.Panel panelRight;
        private System.Windows.Forms.Panel panelPickerHost;
        private System.Windows.Forms.TabControl tabsPicker;
        private System.Windows.Forms.TabPage tabKeyboard;
        private System.Windows.Forms.Panel panelKbScroll;
        private KeyboardOG.Keyboard keyboard1;
        private System.Windows.Forms.TabPage tabMouse;
        private KeyboardOG.MouseControl mousePicker;
        private System.Windows.Forms.TabPage tabGamepad;
        private KeyboardOG.GamepadControl gamepadPicker;
        private System.Windows.Forms.TabPage tabConsumer;
        private KeyboardOG.ConsumerControl consumerPicker;
        private System.Windows.Forms.Label lblPickerHint;
        private KeyboardOG.HallParamControl hallPanel;
        private System.Windows.Forms.Panel panelLeft;
        private System.Windows.Forms.GroupBox grpSlots;
        private System.Windows.Forms.ListView lvBindings;
        private System.Windows.Forms.ColumnHeader colSlotIndex;
        private System.Windows.Forms.ColumnHeader colSlotText;
        private System.Windows.Forms.Panel panelBindBtns;
        private System.Windows.Forms.Button btnSlotUp;
        private System.Windows.Forms.Button btnSlotDown;
        private System.Windows.Forms.Button btnSlotDelete;
        private System.Windows.Forms.Button btnSlotClear;
        private System.Windows.Forms.Panel panelSlotTitle;
        private System.Windows.Forms.Button btnClearAllLocal;
        private System.Windows.Forms.Panel panelSync;
        private System.Windows.Forms.Button btnSyncMaps;
        private System.Windows.Forms.Button btnSyncParams;
        private System.Windows.Forms.Label lblKeyHint;
        private System.Windows.Forms.Label lblConnBadge;
        private System.Windows.Forms.Label lblSlotTitle;
        private System.Windows.Forms.GroupBox grpKey;
        private System.Windows.Forms.FlowLayoutPanel flpKeys;
        private System.Windows.Forms.Label lblKeyReserved;
    }
}
