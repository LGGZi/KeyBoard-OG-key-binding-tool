using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KeyboardOG
{
    public partial class Form1 : Form
    {
        /// <summary>
        /// 把后台线程的事件切回 UI 线程。
        /// 之所以不用 SynchronizationContext.Current：窗体构造函数阶段它还没安装。
        /// </summary>
        private sealed class ControlSyncContext : SynchronizationContext
        {
            private readonly Control _owner;
            public ControlSyncContext(Control owner) { _owner = owner; }

            public override void Post(SendOrPostCallback d, object state)
            {
                if (_owner.IsDisposed || !_owner.IsHandleCreated) return;
                try { _owner.BeginInvoke(d, state); }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
            }
        }

        private readonly CommandLogStore _log;
        private readonly HidTransport _transport;
        private readonly KeyMapModel _model = new KeyMapModel();
        private readonly Button[] _keyButtons = new Button[DeviceLimits.KeyCount];
        private readonly List<IPickerControl> _pickers = new List<IPickerControl>();

        private LogForm _logForm;
        private int _currentKey;

        /// <summary>可调参数（发送频率 / 超时 / 是否等 FIN），存在 exe 目录的 ini 里。</summary>
        private AppSettings _settings;

        /// <summary>从键盘查回来的设备信息（连接成功后自动查询）。查询之前是 null。</summary>
        private DeviceInfo _deviceInfo;

        /// <summary>当前实际要显示的按键数量：以键盘回报的 BUTTONCOUNT 为准，查不到就用配置值。</summary>
        private int _keyCount = DeviceLimits.KeyCount;

        /// <summary>
        /// 每个按键"本地是否改过还没写进键盘"。
        ///   false（绿）= 查询同步过 / 刚写入成功，本地和键盘一致
        ///   true （红）= 本地加删过映射，还没写入
        /// 注意：单键写入后**不做查询**（查询会把别的按键的本地内容一起覆盖）。
        /// </summary>
        private readonly bool[] _keyDirty = new bool[DeviceLimits.KeyCount];

        /// <summary>"清空全部按键映射"要点两次才生效，这个记第一次点过了。</summary>
        private bool _clearAllArmed;

        /// <summary>
        /// 下拉框里每一项对应的**原生设备表下标**。
        /// 下拉框是过滤过的（只留指令接口），所以不能直接拿 SelectedIndex 去选设备 ——
        /// 那样会选到原生表里同下标的另一个设备（之前就误连到鼠标上过）。
        /// </summary>
        private readonly List<int> _deviceNativeIndex = new List<int>();

        /// <summary>定时把"再次点击以确定清除"的提示收回去（点一次之后没再点就复位）。</summary>
        private readonly System.Windows.Forms.Timer _clearAllTimer = new System.Windows.Forms.Timer();

        /// <summary>从键盘查回来的磁轴参数缓存 [key, 参数下标]。</summary>
        private readonly ushort[,] _paramCache = new ushort[DeviceLimits.KeyCount, 4];
        private readonly bool[,] _paramKnown = new bool[DeviceLimits.KeyCount, 4];
        private static readonly KeyParameter[] ParamOrder =
        {
            KeyParameter.RtActive, KeyParameter.RtDeactive,
            KeyParameter.UpperDeadzone, KeyParameter.LowerDeadzone,
        };
        private bool _suppressSlotEvents;
        private bool _busy;

        // 复用字体对象（每次刷新都 new Font 会泄漏 GDI 句柄）
        private readonly Font _keyFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular);
        private readonly Font _keyFontSmall = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular);

        //批量发送的间隔不再写死：见 AppSettings.MinGapMs（由"发送频率"换算），
        //并且每条指令都会等键盘回 FIN 之后再发下一条，避免丢指令

        public Form1()
        {
            InitializeComponent();

            //读设置文件；不存在就用默认值并自动生成（默认 = 现在能正常工作的那套）
            _settings = AppSettings.Load();

            var ui = new ControlSyncContext(this);
            _log = new CommandLogStore(ui);
            _transport = new HidTransport(_log, ui);
        }

        // ============================================================
        // 初始化
        // ============================================================
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // 设备路径里带 '&'（例如 VID_2341&PID_8036&col02#5&1d66e438&0&0001），
            // 而 Label 默认会把 '&' 当助记符吃掉，显示就会缺字符。
            // 统一关掉助记符，让路径原样显示、方便区分两块相同的板子。
            DisableLabelMnemonics(this);

            _transport.Start();
            _transport.ConnectionChanged += OnConnectionChanged;
            _transport.QueueLengthChanged += OnQueueLengthChanged;
            _log.Updated += OnLogEntryChanged;

            _logForm = new LogForm();
            _logForm.Attach(_log);

            BuildKeyButtons();
            InitPickers();
            WireEvents();

            SelectKey(0);
            SetStatus("就绪。请先点“刷新设备”，选中带 ★ 的指令接口，再点“连接”。");

            RestoreHint();      //提示行 = 磁轴参数说明（校准时会被临时替换）

            _log.Add(LogKind.Info, "启动",
                "KeyboardOG 改键工具已启动。协议：Report ID 0x04，8 字节指令。",
                null, LogState.None);

            // 等界面完全显示后再去枚举，避免关掉窗体时还在枚举。
            BeginInvoke(new Action(async () => await RefreshDevicesAsync()));
        }

        /// <summary>
        /// 生成按键按钮。
        ///
        /// 数量以键盘回报的 BUTTONCOUNT 为准（连接后查询设备信息得到），
        /// 查不到或者比本地配置还大时退回 DeviceLimits.KeyCount
        /// —— 本地列表 / 参数缓存都是按这个上限分配的，不能超过。
        /// </summary>
        private void BuildKeyButtons()
        {
            flpKeys.Controls.Clear();
            foreach (var b in _keyButtons) if (b != null) b.Dispose();
            flpKeys.SuspendLayout();

            for (int i = 0; i < _keyCount; i++)
            {
                var b = new Button
                {
                    Text = i.ToString(),
                    Tag = i,
                    Size = new Size(42, 30),
                    Margin = new Padding(3),
                    FlatStyle = FlatStyle.Flat,
                    TabStop = false,
                    UseVisualStyleBackColor = false,
                    BackColor = Color.White,
                };
                b.FlatAppearance.BorderColor = Color.Gray;
                b.Click += KeyButton_Click;
                flpKeys.Controls.Add(b);
                _keyButtons[i] = b;
            }

            flpKeys.ResumeLayout();
        }

        /// <summary>递归把界面里所有 Label 的助记符解析关掉，保证 '&' 原样显示。</summary>
        private static void DisableLabelMnemonics(Control root)
        {
            var label = root as Label;
            if (label != null) label.UseMnemonic = false;

            foreach (Control c in root.Controls) DisableLabelMnemonics(c);
        }

        private void InitPickers()
        {            _pickers.Add(keyboard1);
            _pickers.Add(mousePicker);
            _pickers.Add(gamepadPicker);
            _pickers.Add(consumerPicker);

            foreach (var p in _pickers)
            {
                p.SelectionMode = true;
                p.Picked += Picker_Picked;
            }
        }

        private void WireEvents()
        {
            btnRefresh.Click += async (s, e) => await RefreshDevicesAsync();
            btnConnect.Click += async (s, e) => await ToggleConnectAsync();
            btnOpenLog.Click += (s, e) => ShowLogForm();
            btnSettings.Click += (s, e) => ShowSettings();

            btnSlotUp.Click += (s, e) => MoveSlot(-1);
            btnSlotDown.Click += (s, e) => MoveSlot(1);
            btnSlotDelete.Click += (s, e) => DeleteSlot();
            btnSlotClear.Click += (s, e) => ClearCurrentKeyLocal();
            btnClearAllLocal.Click += (s, e) => ClearAllLocalClicked();
            btnSyncMaps.Click += async (s, e) => await SyncMappingsAsync();
            //"重新读取磁轴参数"按钮已经挪到磁轴参数面板里了（hallPanel.SyncParametersRequested）

            //点了别的地方就把"再次点击以确定"的待确认状态收回去
            _clearAllTimer.Interval = 4000;
            _clearAllTimer.Tick += (s, e) => ResetClearAllLocal();
            lvBindings.SelectedIndexChanged += (s, e) => ResetClearAllLocal();

            // ① 主操作：把当前按键的映射整片写进去
            btnWriteKey.Click += async (s, e) => await WriteCurrentKeyAsync();
            // ② 全局操作：所有按键各写一遍
            btnWriteAll.Click += async (s, e) => await WriteAllKeysAsync();
            // ③ 危险操作（单独隔离）
            btnClearAll.Click += async (s, e) => await ClearAllOnDeviceAsync();

            // 磁轴专用部分（独立控件）
            hallPanel.WriteRequested += async () => await WriteParametersAsync();
            hallPanel.CalibrateRequested += async mode => await CalibrateAsync(mode);
            hallPanel.SyncParametersRequested += async () => await SyncParametersAsync();
            hallPanel.SelectionChanged += () => RefreshParamDisplay();

            lvBindings.SelectedIndexChanged += (s, e) => UpdateSlotButtons();

            FormClosing += OnFormClosing;
        }

        /// <summary>
        /// 打开指令日志子窗口。
        ///
        /// 注意：必须是「非阻塞、不抢焦点」——
        ///   · 用 Show() 而不是 ShowDialog()，主窗口照常可交互；
        ///   · LogForm 重写了 ShowWithoutActivation，显示时不会被激活；
        ///   · 已经开着的时候什么都不做（不 Activate、不 BringToFront），
        ///     免得用户每点一次按钮主窗口就掉一次焦点。
        /// </summary>
        private void ShowLogForm()
        {
            if (_logForm == null || _logForm.IsDisposed)
            {
                _logForm = new LogForm();
                _logForm.Attach(_log);
            }
            if (!_logForm.Visible) _logForm.Show(this);
        }

        /// <summary>
        /// 打开参数设置。用 ShowDialog 做成模态：独占聚焦、一直在主界面前方，
        /// 不点确定/取消回不到主界面；取消就完全不生效（在副本上改）。
        /// </summary>
        private void ShowSettings()
        {
            using (var dlg = new SettingsForm(_settings))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                _settings = dlg.Result.Clone();
                try
                {
                    _settings.Save();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "设置文件保存失败：\r\n" + ex.Message + "\r\n\r\n本次设置只在这次运行里有效。",
                        "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                SetStatus("设置已生效并保存：" + _settings.ToString() + "    （" + AppSettings.FilePath + "）");
                _log.Add(LogKind.Info, "设置", _settings.ToString() + " / " + AppSettings.FilePath, null, LogState.Done);
            }
        }

        /// <summary>
        /// 发一条指令并等它的 FIN。
        ///
        /// 下位机对一条指令回两条：先 ACK（收到）再 FIN（做完）。
        /// **两条都要读走**，否则残留的回应会被下一条指令当成自己的回应 ——
        /// 这就是之前"上位机指令丢一半"的根源。
        /// 返回值表示是否收到并对上了 FIN。
        /// </summary>
        private async Task<bool> SendCommandAsync(CommandRequest req)
        {
            if (ResponseTimeout <= 0)
            {
                //设置里关掉了"等 FIN"：只发不等（不推荐）
                await _transport.SendAsync(req);
                return true;
            }

            var outcome = await _transport.SendAndCollectAsync(req, _settings.ResponseTimeoutMs, null,
                                                               _settings.ResponseTimeoutMs, null);

            if (outcome == null || !outcome.Sent)
            {
                SetStatus("指令发送失败：" + req.Detail);
                return false;
            }
            if (outcome.Mismatch != null)
            {
                SetStatus("⚠ " + outcome.Mismatch);
                return false;
            }
            if (!outcome.SawFin)
            {
                SetStatus(string.Format("等待 {0} ms 没有收到 FIN：{1}（键盘可能没在处理指令）",
                    _settings.ResponseTimeoutMs, req.Detail));
                return false;
            }
            if (outcome.StaleFirst)
            {
                //管道里有上一条的残留，说明之前有指令没被正确回收
                _log.Add(LogKind.Error, "回应错位",
                    "收到的第一条回应不是本条指令的，管道里有残留：" + req.Detail, req.Payload, LogState.Failed);
            }
            return true;
        }

        /// <summary>
        /// 写操作统一入口：发指令 → 等 FIN → 末尾补一条 CMD_REFRESH。
        ///
        /// CMD_REFRESH 让下位机把 EEPROM 重新读进内存，
        /// 所以改完映射 / 参数不用再重新插拔键盘。
        /// </summary>
        private async Task<bool> SendWriteAsync(CommandRequest req, RefreshTarget refresh)
        {
            bool ok = await SendCommandAsync(req);
            if (!ok) return false;

            await SendCommandAsync(CommandRequest.Of("CMD_REFRESH",
                refresh == RefreshTarget.Mapping ? "刷新按键映射（EEPROM → 内存）" : "刷新磁轴参数（EEPROM → 内存）",
                CommandBuilder.Refresh(refresh), 0));
            return true;
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_busy && MessageBox.Show(this,
                    "还有指令正在下发，现在退出可能会让键盘停在中间状态。仍然退出？",
                    "确认退出", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            try { _transport.Dispose(); } catch { }
            _keyFont.Dispose();
            _keyFontSmall.Dispose();
        }

        // ============================================================
        // 设备 / 连接
        // ============================================================
        private async Task RefreshDevicesAsync()
        {
            cboDevices.Items.Clear();
            _deviceNativeIndex.Clear();
            SetStatus("正在枚举 HID 设备…");

            try
            {
                var all = await _transport.EnumerateAsync();

                if (all.Count == 0)
                {
                    SetStatus("没有发现任何 HID 设备。请确认键盘已插好。");
                    return;
                }

                //下拉框里只留"指令接口"（UsagePage=0xFF00 / Usage=0x01）的设备，
                //其余（触摸板、音频、鼠标之类）不往列表里塞，免得选错。
                int cmdCount = all.Count(d => d.IsCommandInterface != 0);

                //万一一个都没匹配到，就退回列出全部（否则用户根本没法连），并说明原因
                bool fallback = cmdCount == 0;

                for (int i = 0; i < all.Count; i++)
                {
                    if (!fallback && all[i].IsCommandInterface == 0) continue;
                    cboDevices.Items.Add(all[i].DisplayText);
                    _deviceNativeIndex.Add(i);      //显示项 -> 原生设备表下标
                }
                cboDevices.SelectedIndex = 0;

                if (fallback)
                {
                    SetStatus(string.Format(
                        "没有找到带指令接口（UsagePage=0xFF00, Usage=0x01）的设备，先列出全部 {0} 个 HID 设备。" +
                        "请自行核对 VID/PID。", all.Count));
                    return;
                }

                string warn = cmdCount > 1
                    ? string.Format("⚠ 有 {0} 个设备都带指令接口（例如键盘 + 触摸板），" +
                                    "只有“#实例号”不同，连接前请确认是你自己的键盘。", cmdCount)
                    : "连接前请核对 VID/PID 是不是你的键盘。";

                SetStatus(string.Format(
                    "设备列表已过滤：共发现 {0} 个 HID 设备，其中 {1} 个是命令接口，已只列出这 {1} 个。{2}",
                    all.Count, cmdCount, warn));
            }
            catch (Exception ex)
            {
                SetStatus("枚举失败：" + ex.Message);
            }
        }

        private async Task ToggleConnectAsync()
        {
            if (_transport.IsConnected)
            {
                await _transport.DisconnectAsync();
                return;
            }

            int index = cboDevices.SelectedIndex;
            if (index < 0 || index >= _deviceNativeIndex.Count)
            {
                MessageBox.Show(this, "请先刷新并选择一个设备。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            //下拉框是过滤过的，要换算回原生设备表的下标
            int native = _deviceNativeIndex[index];

            SetBusy(true);
            string err = await _transport.ConnectAsync(native);
            SetBusy(false);

            if (err != null)
            {
                MessageBox.Show(this, "打开设备失败：\r\n" + err, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnConnectionChanged(bool connected, string message)
        {
            lblConnState.Text = message;
            lblConnState.ForeColor = connected
                ? Color.FromArgb(20, 120, 60)
                : Color.DimGray;
            btnConnect.Text = connected ? "断开" : "连接";

            //醒目一点的连接状态徽标
            lblConnBadge.Text = connected ? "● 设备已连接" : "● 设备未连接";
            lblConnBadge.BackColor = connected
                ? Color.FromArgb(30, 150, 70)
                : Color.FromArgb(200, 40, 40);

            btnWriteAll.Text = string.Format("写入全部按键映射（0 ~ {0}）", _keyCount - 1);
            UpdateActionButtons();

            // 连接成功后自动跑一次全局查询，把键盘上现有的映射和磁轴参数同步过来。
            // （用 BeginInvoke 是为了让连接流程先走完，再开始收数据。）
            if (connected) BeginInvoke(new Action(async () => await RunStartupSyncAsync()));
        }

        private void OnQueueLengthChanged(int pending)
        {
            lblQueue.Text = "队列：" + pending;
        }

        private void OnLogEntryChanged(LogEntry e)
        {
            // 让状态栏跟着最后一条指令的状态走，便于观察“执行中 → 已完成”。
            if (e == null || _busy) return;
            if (e.State == LogState.Done)
                SetStatus(string.Format("[{0}] {1} 已完成", e.Command, e.Detail));
            else if (e.State == LogState.Timeout)
                SetStatus(string.Format("[{0}] {1} —— 超时无回应（键盘可能没回 ACK，或不支持该指令）", e.Command, e.Detail));
            else if (e.State == LogState.Failed)
                SetStatus(string.Format("[{0}] {1} —— 发送失败：{2}", e.Command, e.Detail, e.Result));
        }

        // ============================================================
        // 按键选择 / 映射列表
        // ============================================================
        private void KeyButton_Click(object sender, EventArgs e)
        {
            SelectKey((int)((Button)sender).Tag);
        }

        private void SelectKey(int key)
        {
            if (key < 0 || key >= _keyCount) return;
            _currentKey = key;
            hallPanel.SelectedKey = key;
            UpdateMappingView();
            RefreshParamDisplay();
        }

        /// <summary>
        /// 可视化选键控件的点击处理。
        ///
        ///   · 点到的映射**已经在列表里** -> 只把**它自己**删掉（不管是左键还是右键、
        ///     也不管它是不是当前选中项）—— "点已有的映射就是取消它"这条认知保持成立；
        ///   · 点到的是**新映射** + 列表里有选中条目 + 左键 -> **替换**选中的那一条
        ///     （绝不会牵连别的条目，想只留一个映射时不用先删再加）；
        ///   · 其余情况（右键 / Shift+左键 / 左键但没选中）-> 追加一条。
        /// </summary>
        private void Picker_Picked(object sender, PickerPickedEventArgs e)
        {
            var picker = (IPickerControl)sender;

            // 键盘修饰键（usage 224~231）要换算成固件认识的 104~111，
            // 才会落到报文里 keyboardKeys[13] 那个修饰键字节上。
            byte wireOffset = BindingCatalog.ToWireOffset(picker.Device, e.Offset);
            var binding = new Binding(picker.Device, wireOffset);

            // ---------- 任何情况下：点到已有的映射 = 删掉它自己 ----------
            int exist = _model.IndexOf(_currentKey, picker.Device, wireOffset);
            if (exist >= 0)
            {
                _model.RemoveAt(_currentKey, exist);
                MarkDirty(_currentKey);
                UpdateMappingView();
                SetStatus(string.Format("按键 #{0} 移除映射：{1}（第 {2} 条）",
                    _currentKey, binding.Text, exist + 1));
                return;
            }

            int sel = SelectedSlot();

            // ---------- 左键 + 列表里有选中条目 = 替换那一条（不动别的）----------
            if (!e.AddOnly && sel >= 0)
            {
                _model.ReplaceAt(_currentKey, sel, binding);
                MarkDirty(_currentKey);
                UpdateMappingView();   //选中的槽位会被保住，不用额外再选
                SetStatus(string.Format("按键 #{0} 第 {1} 条替换为：{2}",
                    _currentKey, sel + 1, binding.Text));
                return;
            }

            // ---------- 追加（右键 / Shift+左键 / 左键但没选中）----------
            if (_model.IsFull(_currentKey))
            {
                SetStatus(string.Format("按键 #{0} 已经有 {1} 条映射，达到上限。请先删除一条。",
                    _currentKey, DeviceLimits.MaxBindingsPerKey));
                System.Media.SystemSounds.Beep.Play();
                return;
            }

            if (!_model.Add(_currentKey, binding))
            {
                SetStatus(string.Format("按键 #{0} 上已经有 {1} 了。", _currentKey, binding.Text));
                return;
            }

            MarkDirty(_currentKey);
            UpdateMappingView();
            SelectSlot(_model.Count(_currentKey) - 1);   //选中刚加进来的那条
            SetStatus(string.Format("按键 #{0} 加入映射：{1}（左键=替换选中项，右键/Shift+左键=追加，点已有的=删除）",
                _currentKey, binding.Text));
        }

        /// <summary>选中映射列表里的第 index 条（越界就当没有选中）。</summary>
        private void SelectSlot(int index)
        {
            _suppressSlotEvents = true;
            try
            {
                lvBindings.SelectedIndices.Clear();
                if (index >= 0 && index < lvBindings.Items.Count)
                    lvBindings.Items[index].Selected = true;
            }
            finally
            {
                _suppressSlotEvents = false;
            }
            UpdateSlotButtons();
        }

        private void UpdateMappingView()
        {
            //重建之前先把选中的槽位记下来：左键"替换选中项"就靠这个选中项，
            //刷新列表不能把它冲掉（原来每次都强制选中第 1 条，替换就没法用了）
            int keep = SelectedSlot();

            _suppressSlotEvents = true;
            try
            {
                lvBindings.BeginUpdate();
                lvBindings.Items.Clear();

                var list = _model[_currentKey];
                for (int i = 0; i < list.Count; i++)
                {
                    var item = new ListViewItem(new[] { (i + 1).ToString(), list[i].Text });
                    item.Tag = i;
                    lvBindings.Items.Add(item);
                }
                lvBindings.EndUpdate();

                if (keep >= 0 && keep < lvBindings.Items.Count)
                    lvBindings.Items[keep].Selected = true;
                else if (lvBindings.Items.Count > 0)
                    lvBindings.Items[0].Selected = true;
            }
            finally
            {
                _suppressSlotEvents = false;
            }

            lblSlotTitle.Text = string.Format("按键 #{0}：{1}/{2} 条",
                _currentKey, _model.Count(_currentKey), DeviceLimits.MaxBindingsPerKey);

            //提示行现在专门用来显示参数说明 / 校准状态（不再显示映射手势提示）

            SyncPickerSelection();
            RefreshKeyButtons();
            UpdateSlotButtons();
            UpdateActionButtons();

            keyboard1.RefreshKeyColors();
        }

        private void SyncPickerSelection()
        {
            foreach (var p in _pickers)
                p.SetSelection(_model.PickerOffsetsOf(_currentKey, p.Device));
        }

        private void RefreshKeyButtons()
        {
            for (int i = 0; i < _keyCount; i++)
            {
                int count = _model.Count(i);
                var b = _keyButtons[i];

                b.Text = count > 0 ? string.Format("{0}·{1}", i, count) : i.ToString();
                b.Font = count > 0 ? _keyFontSmall : _keyFont;

                //底色只表示"选没选中"：选中=蓝，没选中=白
                //数字颜色表示映射状态：绿 = 和键盘一致，红 = 本地改过还没写入
                bool sel = (i == _currentKey);
                b.BackColor = sel ? Color.FromArgb(150, 200, 255) : Color.White;
                b.ForeColor = _keyDirty[i] ? Color.FromArgb(190, 40, 40) : Color.FromArgb(20, 120, 60);
                b.FlatAppearance.BorderColor = sel ? Color.FromArgb(30, 70, 150) : Color.FromArgb(170, 170, 170);
                b.FlatAppearance.BorderSize = sel ? 2 : 1;
            }
        }

        private int SelectedSlot()
        {
            if (lvBindings.SelectedIndices.Count == 0) return -1;
            return lvBindings.SelectedIndices[0];
        }

        private void UpdateSlotButtons()
        {
            if (_suppressSlotEvents) return;
            bool has = SelectedSlot() >= 0;
            btnSlotUp.Enabled = has;
            btnSlotDown.Enabled = has;
            btnSlotDelete.Enabled = has;
            btnSlotClear.Enabled = _model.Count(_currentKey) > 0;
        }

        private void MoveSlot(int delta)
        {
            int index = SelectedSlot();
            if (index < 0) return;

            _model.Move(_currentKey, index, delta);
            MarkDirty(_currentKey);
            UpdateMappingView();

            int target = index + delta;
            if (target >= 0 && target < lvBindings.Items.Count)
                lvBindings.Items[target].Selected = true;
        }

        private void DeleteSlot()
        {
            int index = SelectedSlot();
            if (index < 0) return;

            _model.RemoveAt(_currentKey, index);
            MarkDirty(_currentKey);
            UpdateMappingView();
            SetStatus("已从本地列表删除一条映射（还没有写入键盘）。");
        }

        /// <summary>清空当前按键的本地映射（不弹窗，本地改动标红）。</summary>
        private void ClearCurrentKeyLocal()
        {
            if (_model.Count(_currentKey) == 0) return;

            _model.ClearKey(_currentKey);
            MarkDirty(_currentKey);
            UpdateMappingView();
            SetStatus(string.Format("已清空按键 #{0} 的本地映射列表（还没写入键盘）。", _currentKey));
        }

        /// <summary>
        /// "清空全部按键映射"：点两次才生效，不弹窗。
        /// 第一次点只是把文字改成"再次点击以确定清除"，几秒内不点第二次就自动复位。
        /// </summary>
        private void ClearAllLocalClicked()
        {
            if (!_clearAllArmed)
            {
                _clearAllArmed = true;
                btnClearAllLocal.Text = "再次点击以确定清除";
                SetStatus("再点一次「再次点击以确定清除」就会清空本地全部按键的映射列表（不影响键盘）。");
                _clearAllTimer.Stop();
                _clearAllTimer.Start();
                return;
            }

            ResetClearAllLocal();

            int total = _model.TotalCount;
            _model.ClearAll();
            for (int i = 0; i < _keyCount; i++) MarkDirty(i);

            UpdateMappingView();
            SetStatus(string.Format("已清空本地全部按键的映射列表（共 {0} 条，还没写入键盘）。", total));
        }

        private void ResetClearAllLocal()
        {
            _clearAllTimer.Stop();
            if (!_clearAllArmed) return;

            _clearAllArmed = false;
            btnClearAllLocal.Text = "清空全部按键映射";
        }

        /// <summary>标记某个按键本地有未写入的改动（按键按钮变红）。</summary>
        private void MarkDirty(int key)
        {
            if (key < 0 || key >= DeviceLimits.KeyCount) return;
            _keyDirty[key] = true;
            RefreshKeyButtons();
        }

        /// <summary>本地与键盘重新一致了（写入成功 / 查询同步过），按键按钮变回绿色。</summary>
        private void MarkAllClean()
        {
            for (int i = 0; i < _keyDirty.Length; i++) _keyDirty[i] = false;
            RefreshKeyButtons();
        }

        // ============================================================
        // 指令下发
        // ============================================================
        /// <summary>等回应的超时：设置里关掉"等 FIN"就返回 0（只发不等）。</summary>
        private int ResponseTimeout
        {
            get { return _settings.WaitForFin ? _settings.ResponseTimeoutMs : 0; }
        }

        private bool RequireConnection()
        {
            if (_transport.IsConnected) return true;
            MessageBox.Show(this, "还没有连接到键盘的指令接口。请先“刷新设备”→“连接”。",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        /// <summary>
        /// 主操作：把「当前按键」的整片映射写进键盘。
        ///
        /// 新存储结构下每个按键有自己固定的一块 EEPROM 区域（MAXIMUM_BINDABLE_KEY 个槽），
        /// 所以直接用 OVERRIDE 把 0..MAX-1 每个槽都写一遍即可：
        ///   · 本地列表里的映射写进对应槽；
        ///   · 多出来的槽写 device = NO_DEVICE，把旧数据清掉（读的时候遇到空槽就停）。
        /// 整个过程不碰别的按键，也不需要先清整表。
        /// </summary>
        private async Task WriteCurrentKeyAsync()
        {
            if (!RequireConnection()) return;

            var list = _model[_currentKey];
            string warning = string.Format(
                "把按键 #{0} 的映射写进键盘：\r\n\r\n" +
                "  共 {1} 条映射{2}\r\n\r\n" +
                "只会改写按键 #{0} 自己的 EEPROM 区域，不影响其它按键；\r\n" +
                "后面的空槽由键盘自动清零，所以只发 {3} 条指令。\r\n" +
                "写完会自动发一条 CMD_REFRESH，不用重新插拔键盘。\r\n\r\n继续？",
                _currentKey, list.Count,
                list.Count == 0 ? "（即清掉这个键的全部映射）" : "",
                Math.Max(1, list.Count));

            if (MessageBox.Show(this, warning, "写入当前按键映射",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            bool ok = await RunBatchAsync(BuildKeyWriteRequests(_currentKey),
                string.Format("写入按键 #{0}", _currentKey), RefreshTarget.Mapping);

            if (ok)
            {
                //只把这一个按键标成"已同步"。这里**故意不做查询**：
                //全局查询会把其它按键的本地内容一起覆盖掉，单键写入不该有这种副作用。
                _keyDirty[_currentKey] = false;
                RefreshKeyButtons();

                SetStatus(string.Format("按键 #{0} 的 {1} 条映射已写入并已刷新（这个按键已变绿，别的按键不受影响）。",
                    _currentKey, list.Count));
            }
        }

        /// <summary>全局操作：把所有按键各按上面的方式写一遍（同样不动 CLEAR，不破坏性）。</summary>
        private async Task WriteAllKeysAsync()
        {
            if (!RequireConnection()) return;

            string warning = string.Format(
                "把本地列表里的全部 {0} 条映射写进键盘（按键 0 ~ {1}）。\r\n\r\n" +
                "每个按键只发本地有的那几条（后面的空槽键盘会自动清零）。\r\n" +
                "本地列表里没有的按键，键盘上对应的映射会被清掉。\r\n" +
                "写完会自动发一条 CMD_REFRESH，不用重新插拔键盘。\r\n\r\n继续？",
                _model.TotalCount, _keyCount - 1, DeviceLimits.MaxBindingsPerKey);

            if (MessageBox.Show(this, warning, "写入全部按键映射",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            var requests = new List<CommandRequest>();
            for (int k = 0; k < _keyCount; k++)
                requests.AddRange(BuildKeyWriteRequests(k));

            bool allOk = await RunBatchAsync(requests, "写入全部按键映射", RefreshTarget.Mapping);
            if (allOk) MarkAllClean();
            SetStatus(allOk
                ? "全部按键已写入并已刷新，所有按键已变绿，可以直接用了。"
                : "全部按键写入过程中有指令没成功，请看日志和状态栏。");
        }

        /// <summary>
        /// 生成「写一个按键」的指令序列。
        ///
        /// 固件在写完第 N 个槽之后会自动把 N 后面的槽全部设成空槽
        /// （EEPROM 里的 keyMapClearFromEEPROM），所以这里只需要按顺序发本地列表里的
        /// 那几条映射，尾部不用再逐条补发空槽。
        /// 列表为空时发一条 device = 0 到槽 0，固件就会把这个键整片清掉。
        /// </summary>
        private List<CommandRequest> BuildKeyWriteRequests(int key)
        {
            var list = _model[key];
            int count = Math.Max(1, list.Count);
            var requests = new List<CommandRequest>(count);

            for (int slot = 0; slot < count; slot++)
            {
                byte device = 0, offset = 0;
                string detail;

                if (slot < list.Count)
                {
                    var b = list[slot];
                    device = (byte)b.Device;
                    offset = b.Offset;
                    detail = string.Format("按键#{0} 槽{1} ← {2}", key, slot, b.Text);
                }
                else
                {
                    detail = string.Format("按键#{0} 槽0 ← 空槽（清空该键）", key);
                }

                requests.Add(CommandRequest.Of("SET_MAP/OVERRIDE", detail,
                    CommandBuilder.SetMap((MapDevice)device, (byte)key, offset,
                                          MapOperation.Override, (byte)slot), 0));
            }
            return requests;
        }

        private async Task ClearAllOnDeviceAsync()
        {
            if (!RequireConnection()) return;

            if (MessageBox.Show(this,
                    "这是危险操作：将清空键盘上【全部按键】的映射（EEPROM 里的整片映射区）。\r\n\r\n" +
                    "本地列表不会被修改，之后可以用“写入全部按键映射”把本地列表重新写回去。\r\n\r\n继续？",
                    "⚠ 清空全部映射", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            var req = CommandRequest.Of("SET_MAP/CLEAR", "清空全部映射（整片映射区）",
                CommandBuilder.SetMap(MapDevice.Keyboard, 0, 0, MapOperation.Clear, 0), 0);

            bool cleared = await SendWriteAsync(req, RefreshTarget.Mapping);
            if (!cleared) return;

            _model.ClearAll();
            MarkAllClean();
            UpdateMappingView();
            SetStatus("已下发清空指令，本地列表也一并清空了（键盘上现在没有任何映射），按键按钮已全部变绿。");
            RefreshParamDisplay();
        }

        // ============================================================
        // 查询：连接成功后自动同步
        //
        // 下位机会把结果逐条发回来（格式与下发指令一致），最后回一个 FIN，
        // 所以这里用 SendAndCollectAsync 一直收到 FIN 为止。
        // ============================================================

        /// <summary>
        /// 连接成功后自动跑：设备信息 → 全部键值 → 全部磁轴参数。
        ///
        /// 这是由 OnConnectionChanged 里 BeginInvoke 起来的 fire-and-forget 后台任务，
        /// 所以整个流程必须自己兜住异常 —— 否则任何一步出错都会变成"UI 线程异常"弹窗
        /// （非磁轴键盘隐藏磁轴面板时就这样炸过一次）。
        /// </summary>
        private async Task RunStartupSyncAsync()
        {
            if (!_transport.IsConnected) return;

            _busy = true;
            SetBusy(true);
            try
            {
                // ---- 0. 设备信息（按键数量 / 是否磁轴 / 编码器数量 …）----
                // 固件回这一帧之后不发 FIN，所以用"收到设备信息帧"当结束条件。
                SetStatus("正在读取键盘的设备信息…");
                var infoOutcome = await _transport.SendAndCollectAsync(
                    CommandRequest.Of("QUERY/DEVICE_INFO", "读取设备信息",
                        CommandBuilder.Query(QueryTarget.DeviceInfo, QueryMode.All, 0), 0),
                    3000, null, 1500, frame => ResponseParser.KindOf(frame) == ResponseKind.DeviceInfo);

                ApplyDeviceInfo(infoOutcome);

                // ---- 1. 键值查询（全局）----
                SetStatus("正在从键盘读取全部按键映射…");
                var mapOutcome = await QueryAsync(QueryTarget.Mapping, QueryMode.All, 0,
                    string.Format("读取全部按键映射（0~{0}）", _keyCount - 1));

                if (mapOutcome == null || !mapOutcome.Sent)
                {
                    SetStatus("同步失败：查询指令没能发出去。");
                    return;
                }

                int skipped;
                int imported = ImportMappings(mapOutcome, out skipped);
                MarkAllClean();
                UpdateMappingView();

                // ---- 2. 磁轴参数查询（全局）----
                // 不是磁轴键盘就不查了（面板也已经隐藏）
                HidTransport.CollectOutcome paramOutcome = null;

                if (_deviceInfo == null || _deviceInfo.HasHallSensor)
                {
                    SetStatus("正在从键盘读取磁轴参数…");
                    paramOutcome = await QueryAsync(QueryTarget.Parameter, QueryMode.All, 0,
                        string.Format("读取全部按键参数（0~{0}）", _keyCount - 1));
                }

                int paramCount = ImportParameters(paramOutcome);
                RefreshParamDisplay();

                string tail = mapOutcome.SawFin && (paramOutcome == null || paramOutcome.SawFin)
                    ? ""
                    : "（有查询没收到 FIN，可能是固件版本较旧或是超时，结果可能不完整）";

                SetStatus(string.Format(
                    "已从键盘同步：{0} 条映射（跳过 {1} 条），{2} 项参数。{3}{4}",
                    imported, skipped, paramCount,
                    _model.TotalCount == 0 && imported == 0 ? "键盘上目前没有任何映射。" : "", tail));

                _log.Add(LogKind.Info, "同步完成",
                    string.Format("从键盘读取到 {0} 条映射、{2} 项参数，跳过 {1} 条。", imported, skipped, paramCount),
                    null, LogState.Done);
            }
            catch (Exception ex)
            {
                //后台同步任务是 fire-and-forget 起来的，出错必须自己兜住
                SetStatus("同步过程中出错：" + ex.Message);
                _log.Add(LogKind.Error, "同步失败", ex.Message, null, LogState.Failed);
            }
            finally
            {
                _busy = false;
                SetBusy(false);
            }
        }

        /// <summary>
        /// 把键值查询的结果导进本地列表（会先清空本地列表）。
        /// outcome 为 null（没查过）时什么都不做 —— 非磁轴键盘就走这条路径。
        /// 返回导入条数，skipped 是丢掉的条数。
        /// </summary>
        private int ImportMappings(HidTransport.CollectOutcome outcome, out int skipped)
        {
            skipped = 0;
            _model.ClearAll();
            if (outcome == null) return 0;

            int imported = 0;
            foreach (var f in outcome.Frames)
            {
                if (ResponseParser.KindOf(f) != ResponseKind.MapItem) continue;

                int key = ResponseParser.MapKey(f);
                if (key < 0 || key >= _keyCount) { skipped++; continue; }

                //device 只可能是 1~4；0xFF / 旧格式残留一律丢掉，不要导进列表
                byte dev = ResponseParser.MapDeviceByte(f);
                if (dev < 1 || dev > 4) { skipped++; continue; }

                if (_model.Add(key, ResponseParser.ToBinding(f))) imported++;
                else skipped++;
            }
            return imported;
        }

        /// <summary>把磁轴参数查询的结果存进缓存。outcome 为 null 时什么都不做。</summary>
        private int ImportParameters(HidTransport.CollectOutcome outcome)
        {
            if (outcome == null) return 0;

            int count = 0;
            foreach (var f in outcome.Frames)
            {
                if (ResponseParser.KindOf(f) != ResponseKind.ParamItem) continue;

                int key = ResponseParser.ParamKey(f);
                if (key < 0 || key >= _keyCount) continue;

                int idx = Array.IndexOf(ParamOrder, ResponseParser.ParamCode(f));
                if (idx < 0) continue;

                _paramCache[key, idx] = ResponseParser.ParamValue(f);
                _paramKnown[key, idx] = true;
                count++;
            }
            return count;
        }

        /// <summary>
        /// 应用键盘回报的设备信息：
        ///   · 按键数量  -> 重建左上角的可选按键按钮（并夹到本地配置的上限）
        ///   · 是否磁轴  -> 显示 / 隐藏右下角的磁轴参数面板
        /// 编码器数量、设备类型、保留位先存进 _deviceInfo，留给之后的功能。
        /// </summary>
        private void ApplyDeviceInfo(HidTransport.CollectOutcome outcome)
        {
            DeviceInfo info = null;
            if (outcome != null)
            {
                foreach (var f in outcome.Frames)
                {
                    if (ResponseParser.KindOf(f) != ResponseKind.DeviceInfo) continue;
                    info = DeviceInfo.FromFrame(f);
                    if (info != null) break;
                }
            }

            if (info == null)
            {
                //查不到就保持原样（用本地配置的按键数、磁轴面板按默认显示）
                _log.Add(LogKind.Info, "设备信息", "没有收到设备信息报文，按本地配置显示。", null, LogState.Timeout);
                SetStatus("没有收到键盘的设备信息，暂按本地配置显示（按键数 " + DeviceLimits.KeyCount + "）。");
                return;
            }

            _deviceInfo = info;

            // ---- 按键数量 ----
            int want = info.KeyCount;
            if (want <= 0) want = DeviceLimits.KeyCount;

            if (want > DeviceLimits.KeyCount)
            {
                SetStatus(string.Format(
                    "键盘报告有 {0} 个按键，超过本地配置的上限 {1}，只显示前 {1} 个（需要改 DeviceLimits.KeyCount 并重新编译上位机）。",
                    want, DeviceLimits.KeyCount));
                want = DeviceLimits.KeyCount;
            }

            if (want != _keyCount)
            {
                _keyCount = want;
                if (_currentKey >= _keyCount) _currentKey = 0;
                BuildKeyButtons();
            }

            hallPanel.SetKeyCount(_keyCount);
            for (int i = _keyCount; i < _keyDirty.Length; i++) _keyDirty[i] = false;

            //执行条上的文案跟着实际按键数量走
            btnWriteAll.Text = string.Format("写入全部按键映射（0 ~ {0}）", _keyCount - 1);
            btnWriteKey.Text = string.Format("写入当前按键映射（全部 {0} 个槽）",
                DeviceLimits.MaxBindingsPerKey);

            // ---- 是否磁轴 ----
            bool hall = info.HasHallSensor;
            if (hallPanel.Visible != hall) hallPanel.Visible = hall;

            _log.Add(LogKind.Info, "设备信息", info.ToString(), info.Raw, LogState.Done);

            if (hall)
            {
                SetStatus(string.Format(
                    "键盘报告：{0} 键 · 磁轴键盘（已显示磁轴参数面板）· 编码器 {1} 个。",
                    _keyCount, info.EncoderCount));
            }
            else
            {
                SetStatus(string.Format(
                    "键盘报告：{0} 键 · 非磁轴键盘（已隐藏磁轴参数面板，只做改键）· 编码器 {1} 个。",
                    _keyCount, info.EncoderCount));
            }

            UpdateMappingView();
        }

        /// <summary>
        /// 「重新读取映射」：重新从键盘读一遍映射，**放弃本地对映射的全部改动**还原成键盘上的内容。
        /// </summary>
        private async Task SyncMappingsAsync()
        {
            if (!RequireConnection()) return;
            if (_busy) { SetStatus("有指令正在执行，等它结束再同步。"); return; }

            _busy = true;
            SetBusy(true);
            try
            {
                SetStatus("正在重新读取全部按键映射…");
                var outcome = await QueryAsync(QueryTarget.Mapping, QueryMode.All, 0,
                    string.Format("重新读取全部按键映射（0~{0}）", _keyCount - 1));

                if (outcome == null || !outcome.Sent)
                {
                    SetStatus("重新读取映射失败：查询指令没能发出去。");
                    return;
                }

                int skipped;
                int imported = ImportMappings(outcome, out skipped);
                MarkAllClean();
                UpdateMappingView();

                SetStatus(string.Format("已重新读取映射：本地恢复成键盘上的 {0} 条（丢弃本地改动），按键按钮全部变绿。",
                    imported));
            }
            catch (Exception ex)
            {
                SetStatus("重新读取映射出错：" + ex.Message);
            }
            finally
            {
                _busy = false;
                SetBusy(false);
            }
        }

        /// <summary>「重新读取磁轴参数」：重新读一遍参数，放弃本地对参数值的改动。</summary>
        private async Task SyncParametersAsync()
        {
            if (!RequireConnection()) return;
            if (_busy) { SetStatus("有指令正在执行，等它结束再同步。"); return; }

            if (_deviceInfo != null && !_deviceInfo.HasHallSensor)
            {
                SetStatus("这台键盘没有磁轴，没有参数可以读取。");
                return;
            }

            _busy = true;
            SetBusy(true);
            try
            {
                SetStatus("正在重新读取磁轴参数…");
                var outcome = await QueryAsync(QueryTarget.Parameter, QueryMode.All, 0,
                    string.Format("重新读取全部按键参数（0~{0}）", _keyCount - 1));

                if (outcome == null || !outcome.Sent)
                {
                    SetStatus("重新读取参数失败：查询指令没能发出去。");
                    return;
                }

                int count = ImportParameters(outcome);
                hallPanel.MarkAllParamsSynced();
                RefreshParamDisplay();
                SetStatus(string.Format("已重新读取磁轴参数：{0} 项，值框和滑块已经换成键盘上的真实值，按键标记也复位了。", count));
            }
            catch (Exception ex)
            {
                SetStatus("重新读取参数出错：" + ex.Message);
            }
            finally
            {
                _busy = false;
                SetBusy(false);
            }
        }

        /// <summary>发一条查询并把结果收到 FIN 为止。</summary>
        private Task<HidTransport.CollectOutcome> QueryAsync(QueryTarget what, QueryMode scope, byte key, string detail)
        {
            var req = CommandRequest.Of("QUERY/" + (what == QueryTarget.Mapping ? "MAPPING" : "PARAM"),
                detail, CommandBuilder.Query(what, scope, key), 0);

            //全局查询最多可能回 14*12 + 14*4 条，给足时间
            int overall = scope == QueryMode.All ? 12000 : 2500;
            return _transport.SendAndCollectAsync(req, overall, null);
        }

        /// <summary>
        /// 把参数缓存里的值显示到磁轴面板上。
        /// 用"焦点按键"：只勾了一个按键就显示那一个的，否则跟着主界面当前按键。
        /// </summary>
        private void RefreshParamDisplay()
        {
            int key = hallPanel.FocusKey;
            if (key < 0 || key >= _keyCount) key = _currentKey;
            if (key < 0 || key >= DeviceLimits.KeyCount) return;

            var values = new int[ParamOrder.Length];
            var known = new bool[ParamOrder.Length];
            for (int i = 0; i < ParamOrder.Length; i++)
            {
                values[i] = _paramCache[key, i];
                known[i] = _paramKnown[key, i];
            }
            hallPanel.ShowCurrentValues(values, known, key);
        }

        /// <summary>磁轴参数面板下面那行提示的默认文字（校准时会临时被替换掉）。</summary>
        private const string ParamHintText =
            "提示：数值 = 满行程的千分比（11~979，40 = 4.0%）；RT 触发行程 / 上死区的滑块是「上=小、下=大」。" +
            "「写入参数」会把四个参数一起发给范围内的按键，写完自动刷新，不用重新插拔。";

        /// <summary>把那行提示恢复成参数说明。</summary>
        private void RestoreHint()
        {
            if (_hintTimer != null) { _hintTimer.Stop(); _hintTimer.Dispose(); _hintTimer = null; }
            lblPickerHint.Text = ParamHintText;
        }

        private System.Windows.Forms.Timer _hintTimer;

        /// <summary>临时把那行提示换成别的文字，ms 毫秒后自动还原成参数说明。</summary>
        private void ShowHintTemporarily(string text, int ms)
        {
            if (_hintTimer != null) { _hintTimer.Stop(); _hintTimer.Dispose(); }
            lblPickerHint.Text = text;

            _hintTimer = new System.Windows.Forms.Timer { Interval = ms };
            _hintTimer.Tick += (s, e) => RestoreHint();
            _hintTimer.Start();
        }

        /// <summary>只安排"过一会儿还原"，不动当前文字（校准进度还在往上写）。</summary>
        private void ScheduleRestoreHint(int ms)
        {
            if (_hintTimer != null) { _hintTimer.Stop(); _hintTimer.Dispose(); }
            _hintTimer = new System.Windows.Forms.Timer { Interval = ms };
            _hintTimer.Tick += (s, e) => RestoreHint();
            _hintTimer.Start();
        }

        // ============================================================
        // 磁轴参数 / 校准（由 HallParamControl 触发）
        // ============================================================
        /// <summary>
        /// 「写入参数」：把滑块上的四个参数发给**选中的那些按键**（没选就是当前按键），
        /// 每个按键逐个发四条 SET_PARAM，最后统一发一条 CMD_REFRESH。
        /// </summary>
        private async Task WriteParametersAsync()
        {
            if (!RequireConnection()) return;
            if (_busy) { SetStatus("有指令正在执行，等它结束再写参数。"); return; }

            var values = hallPanel.ParameterValues;
            var keys = hallPanel.BatchKeys;
            bool single = keys.Count == 0;
            if (single) keys.Add(_currentKey);

            string scope = single
                ? string.Format("当前按键 #{0}", _currentKey)
                : string.Format("选中的 {0} 个按键（{1}）", keys.Count, string.Join("/", keys.ConvertAll(k => "#" + k).ToArray()));

            if (MessageBox.Show(this,
                    string.Format("把四个参数写入 {0}？\r\n\r\n{1}\r\n\r\n每条指令都会等键盘回 FIN，写完全局刷新一次，不用重新插拔。\r\n\r\n继续？",
                        scope,
                        string.Join("\r\n", Array.ConvertAll(ParamOrder, p =>
                            string.Format("  {0} = {1}（{2}）",
                                BindingCatalog.Parameter(p).Name,
                                values[Array.IndexOf(ParamOrder, p)],
                                BindingCatalog.FormatParameterValue(values[Array.IndexOf(ParamOrder, p)]))))),
                    "写入磁轴参数", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            _busy = true;
            SetBusy(true);
            try
            {
                int failed = 0, done = 0;
                int total = keys.Count * ParamOrder.Length;

                var keyOk = new List<int>();      //四个参数全写成功的按键
                foreach (int key in keys)
                {
                    int keyFailed = 0;
                    for (int i = 0; i < ParamOrder.Length; i++)
                    {
                        var param = ParamOrder[i];
                        int value = values[i];

                        if (value < DeviceLimits.ParamValueMin || value > DeviceLimits.ParamValueMax)
                        {
                            SetStatus(string.Format("参数值 {0} 超出固件接受范围（{1}~{2}），已跳过。",
                                value, DeviceLimits.ParamValueMin, DeviceLimits.ParamValueMax));
                            continue;
                        }

                        var req = CommandRequest.Of("SET_PARAM",
                            string.Format("按键#{0} {1} = {2}（{3:0.0}% 行程）",
                                key, BindingCatalog.Parameter(param).Name, value, value / 10.0),
                            CommandBuilder.SetParameter(param, (byte)key, (ushort)value), 0);

                        done++;
                        SetStatus(string.Format("正在写入参数：{0}/{1}  {2}", done, total, req.Detail));
                        if (!await SendCommandAsync(req)) { failed++; keyFailed++; }

                        if (done < total) await Task.Delay(_settings.MinGapMs);
                    }

                    if (keyFailed == 0) keyOk.Add(key);
                }

                //写完统一刷新一次
                await SendCommandAsync(CommandRequest.Of("CMD_REFRESH", "刷新磁轴参数（EEPROM → 内存）",
                    CommandBuilder.Refresh(RefreshTarget.Parameter), 0));

                //把本地缓存更新成刚写进去的值，界面显示跟着走
                foreach (int key in keys)
                {
                    for (int i = 0; i < ParamOrder.Length; i++)
                    {
                        _paramCache[key, i] = (ushort)values[i];
                        _paramKnown[key, i] = true;
                    }
                }
                //只有四个参数都写成功的按键才把红字恢复成绿字
                hallPanel.MarkParamsSynced(keyOk);
                RefreshParamDisplay();

                if (failed == 0)
                {
                    ShowHintTemporarily(string.Format("参数已写入 {0}，这些按键的数字已经恢复成绿色。", scope), 4000);
                    SetStatus(string.Format("参数已写入 {0} 并已刷新，可以直接用了。", scope));
                }
                else
                {
                    //写失败时红色会保留 —— 明确说明原因，免得以为是界面没刷新
                    ShowHintTemporarily(string.Format(
                        "⚠ 有 {0} 条参数没有写成功（没收到正确的 FIN），对应按键的数字仍然是红色。请检查连接和设备是否选对。",
                        failed), 8000);
                    SetStatus(string.Format("参数写入完成，但有 {0} 条没成功，对应按键仍标红（见日志）。", failed));
                }
            }
            finally
            {
                _busy = false;
                SetBusy(false);
            }
        }

        /// <summary>
        /// 校准。
        ///
        /// 下位机的校准是阻塞的：一条指令下去，它要等用户「按住 → 松开」才返回。
        /// 所以这里用 SendAndCollectAsync 一直等到 FIN ——
        ///   · 单键校准：校准完回一个 FIN；
        ///   · 全部校准：每校准完一个键回一个带按键号的 FIN，收到最后一个才算完。
        /// 等待期间状态栏显示“校准中”，执行按钮全部锁上。
        /// </summary>
        private async Task CalibrateAsync(CalibrateMode mode)
        {
            if (!RequireConnection()) return;

            string warning = mode == CalibrateMode.All
                ? "校准全部按键：\r\n\r\n" +
                  "固件会从按键 0 开始逐个校准，每个键都需要你【先按住再松开】。\r\n" +
                  "整个过程中固件的主循环被占用，其它指令不会被处理。\r\n" +
                  "请预留大约 " + _keyCount + " × 每个键 2 秒 的时间，中途不要拔线。\r\n\r\n开始校准？"
                : string.Format("校准当前按键 #{0}：\r\n\r\n请【先按住再松开】这个键，固件会记录行程的上下限。\r\n\r\n开始校准？", _currentKey);

            if (MessageBox.Show(this, warning, "磁轴校准",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            bool isAll = mode == CalibrateMode.All;
            var req = CommandRequest.Of("CALIBRATE",
                isAll ? "校准全部按键" : string.Format("校准按键 #{0}", _currentKey),
                CommandBuilder.Calibrate(mode, (byte)_currentKey), 0);

            //单键给 1 分钟；全部按键按每键 20 秒预留。
            //注意：帧间隔必须给足 —— 校准期间下位机会一直等到用户按键才回下一帧。
            int overall = isAll ? _keyCount * 20000 : 60000;
            const int calibFrameGapMs = 15000;

            int lastKey = -1;
            SetCalibrating(true);
            try
            {
                //提示行临时变成校准状态，校准完再还原
                lblPickerHint.Text = isAll
                    ? "校准中：请从按键 #0 开始，依次「按住 → 松开」每一个键（键盘会逐个回报进度）…"
                    : string.Format("校准中：请对按键 #{0}「按住 → 松开」…", _currentKey);

                SetStatus(isAll
                    ? "校准中：请从按键 #0 开始，依次“按住 → 松开”每一个键（键盘会逐个回报进度）…"
                    : string.Format("校准中：请对按键 #{0}“按住 → 松开”…", _currentKey));

                var outcome = await _transport.SendAndCollectAsync(req, overall, frame =>
                {
                    //每收到一个带按键号的 FIN，就更新一次进度（工作线程回调 → 切回 UI）
                    if (ResponseParser.KindOf(frame) != ResponseKind.Fin) return;
                    int k = ResponseParser.FinKey(frame);
                    BeginInvoke(new Action(() =>
                    {
                        lastKey = k;
                        lblPickerHint.Text = string.Format("校准中：按键 #{0} 已完成（{1}/{2}），请继续按下一个键…",
                            k, k + 1, _keyCount);
                        SetStatus(string.Format("校准中：按键 #{0} 已完成（{1}/{2}）…",
                            k, k + 1, _keyCount));
                    }));
                }, calibFrameGapMs, frame =>
                {
                    //结束条件只对 FIN 生效（ACK 和数据帧一律不算结束）
                    if (ResponseParser.KindOf(frame) != ResponseKind.Fin) return false;
                    //单键校准：一个 FIN 就结束；
                    //全部校准：每个键回一个 FIN，只有最后一个按键的 FIN 才算结束
                    if (!isAll) return true;
                    return ResponseParser.FinKey(frame) == _keyCount - 1;
                });

                if (!outcome.Sent)
                {
                    lblPickerHint.Text = "校准失败：指令没能发出去。";
                    SetStatus("校准指令没能发出去。");
                    return;
                }

                if (!outcome.SawFin)
                {
                    lblPickerHint.Text = "校准等待超时：没有收到键盘的完成回报（FIN）。";
                    SetStatus("校准等待超时：没有收到键盘的完成回报（FIN）。可能是没按到位，或者固件版本不支持 FIN。");
                    return;
                }

                lblPickerHint.Text = isAll
                    ? "校准完成。"
                    : string.Format("按键 #{0} 校准完成。", _currentKey);
                SetStatus(isAll
                    ? string.Format("全部校准完成：最后完成的是按键 #{0}。建议重新插拔键盘一次。", lastKey)
                    : string.Format("按键 #{0} 校准完成。建议重新插拔键盘一次。", _currentKey));

                //校准改的是 EEPROM 里的磁轴参数，末尾补一条刷新让它立刻生效
                await SendCommandAsync(CommandRequest.Of("CMD_REFRESH", "刷新磁轴参数（EEPROM → 内存）",
                    CommandBuilder.Refresh(RefreshTarget.Parameter), 0));

                _log.Add(LogKind.Info, "校准完成",
                    isAll ? "全部按键校准完成" : string.Format("按键 #{0} 校准完成", _currentKey),
                    null, LogState.Done);
            }
            finally
            {
                SetCalibrating(false);
                ScheduleRestoreHint(4000);   //校准状态再留一会儿，然后还原成参数说明
            }
        }

        // ============================================================
        // 批量执行 & 界面状态
        // ============================================================
        /// <summary>
        /// 批量执行：每条都等 FIN 回来再发下一条，两条之间再按设置里的频率歇一下，
        /// 最后补一条 CMD_REFRESH，让下位机把 EEPROM 重新读进内存（不用重新插拔）。
        /// </summary>
        private async Task<bool> RunBatchAsync(List<CommandRequest> requests, string what, RefreshTarget refresh)
        {
            if (_busy)
            {
                MessageBox.Show(this, "有批量指令正在执行，请等它结束。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            _busy = true;
            SetBusy(true);
            try
            {
                int failed = 0;
                for (int i = 0; i < requests.Count; i++)
                {
                    SetStatus(string.Format("正在{0}：{1}/{2}  {3}",
                        what, i + 1, requests.Count, requests[i].Detail));

                    if (!await SendCommandAsync(requests[i])) failed++;

                    //两条之间的最小间隔（发送频率换算出来的）
                    if (i < requests.Count - 1)
                        await Task.Delay(_settings.MinGapMs);
                }

                //末尾的刷新指令：改完映射不用重新插拔
                await SendCommandAsync(CommandRequest.Of("CMD_REFRESH",
                    refresh == RefreshTarget.Mapping ? "刷新按键映射（EEPROM → 内存）" : "刷新磁轴参数（EEPROM → 内存）",
                    CommandBuilder.Refresh(refresh), 0));

                if (failed > 0)
                {
                    SetStatus(string.Format("{0} 完成，但有 {1} 条指令没有收到正确的 FIN（见日志）。",
                        what, failed));
                }
                return failed == 0;
            }
            finally
            {
                _busy = false;
                SetBusy(false);
            }
        }

        private void SetStatus(string text)
        {
            lblStatus.Text = text;
        }

        private void SetBusy(bool busy)
        {
            btnRefresh.Enabled = !busy;
            btnConnect.Enabled = !busy;
            cboDevices.Enabled = !busy;
            UpdateActionButtons();
            Cursor = busy ? Cursors.AppStarting : Cursors.Default;
        }

        private void UpdateActionButtons()
        {
            bool on = _transport.IsConnected && !_busy;

            btnWriteKey.Enabled = on;
            btnWriteAll.Enabled = on;
            btnClearAll.Enabled = on;
            hallPanel.SetEnabledState(on);
        }

        /// <summary>校准过程中把执行按钮锁上，避免用户在阻塞操作里又去点写映射。</summary>
        private void SetCalibrating(bool calibrating)
        {
            if (calibrating)
            {
                btnWriteKey.Enabled = false;
                btnWriteAll.Enabled = false;
                btnClearAll.Enabled = false;
                hallPanel.SetEnabledState(false);
            }
            else
            {
                UpdateActionButtons();
            }
        }

        private void hallPanel_Load(object sender, EventArgs e)
        {

        }
    }
}