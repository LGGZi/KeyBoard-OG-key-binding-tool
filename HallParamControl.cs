using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace KeyboardOG
{
    /// <summary>
    /// 磁轴参数 + 校准控件。
    ///
    /// 从左到右：① 校准按键 + 说明 ② 写入范围（按键单选/多选 + 全选 + 重新读取参数）
    ///           ③ 四个参数各一份「垂直滑块 + 值框」 ④ 写入参数
    ///
    /// 按键按钮的颜色（和左上角那排按键一个含义）：
    ///   绿 = 参数与键盘一致 · 蓝 = 选中但没改过 · 红 = 改过还没写入
    ///
    /// 滑块方向：RT 触发行程 / 上死区 是**上=最小值、下=最大值**（行程越深值越大），
    /// 另外两个是常规的 上=最大值、下=最小值。
    /// </summary>
    public class HallParamControl : UserControl
    {
        private const int ParamCount = 4;
        private const int ColX0 = 648;      //第一列参数的 x
        private const int ColW = 88;        //每列宽度
        private const int BarH = 96;        //垂直滑块高度（加长了）
        private const int MinV = DeviceLimits.ParamValueMin;
        private const int MaxV = DeviceLimits.ParamValueMax;

        /// <summary>哪些参数的滑块要反过来（上 = 最小值）。顺序同 BindingCatalog.Parameters。</summary>
        private static readonly bool[] Invert = { true, false, true, false };

        private readonly GroupBox _grp = new GroupBox();

        // ---- ① 校准 ----
        private readonly Label _lblCalibTitle = new Label();
        private readonly Button _btnCalibKey = new Button();
        private readonly Button _btnCalibAll = new Button();
        private readonly Label _lblCalibNote = new Label();

        // ---- ② 写入范围 ----
        private readonly Label _lblBatchTitle = new Label();
        private readonly FlowLayoutPanel _flpBatch = new FlowLayoutPanel();
        private readonly Button _btnBatchAll = new Button();
        private readonly Button _btnSyncParams = new Button();
        private readonly List<Button> _batchButtons = new List<Button>();
        private readonly HashSet<int> _selectedKeys = new HashSet<int>();

        // ---- ③ 参数 ----
        private readonly Label[] _lblParamName = new Label[ParamCount];
        private readonly Label[] _lblParamDevice = new Label[ParamCount];
        private readonly TrackBar[] _bar = new TrackBar[ParamCount];
        private readonly NumericUpDown[] _num = new NumericUpDown[ParamCount];
        private bool _syncInputs;                       //防止滑块和值框互相触发
        private readonly bool[] _paramDirty = new bool[DeviceLimits.KeyCount];

        // ---- ④ 写入 ----
        private readonly Button _btnWrite = new Button();
        private readonly Label _lblWriteNote = new Label();

        /// <summary>请求写入参数：主窗体自己来读 ParameterValues 和 BatchKeys。</summary>
        public event Action WriteRequested;

        /// <summary>请求校准（All = 全部按键，Single = 当前按键）。</summary>
        public event Action<CalibrateMode> CalibrateRequested;

        /// <summary>请求重新读取磁轴参数（这个按钮已经挪进本控件）。</summary>
        public event Action SyncParametersRequested;

        /// <summary>按键的选中情况变了（主窗体要按焦点按键刷新滑块上的值）。</summary>
        public event Action SelectionChanged;

        private int _selectedKey;       //主界面当前选中的按键
        private int _focusKey;          //滑块上正在显示哪个按键的值（用于标题）

        public HallParamControl()
        {
            BackColor = Color.FromArgb(245, 245, 245);
            Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular, GraphicsUnit.Point, 134);
            AutoScaleMode = AutoScaleMode.None;
            Size = new Size(1108, 190);

            _grp.Text = "磁轴参数 / 校准（当前 #0）";
            _grp.Dock = DockStyle.Fill;

            BuildCalibration();
            BuildBatchPicker();
            BuildParamColumns();
            BuildWriteButton();

            Controls.Add(_grp);
            RefreshBatchColors();
        }

        // ------------------------------------------------------------
        // ① 校准
        // ------------------------------------------------------------
        private void BuildCalibration()
        {
            _lblCalibTitle.AutoSize = true;
            _lblCalibTitle.Location = new Point(10, 22);
            _lblCalibTitle.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            _lblCalibTitle.Text = "校准：";

            _btnCalibKey.Text = "校准当前按键";
            _btnCalibKey.Location = new Point(10, 42);
            _btnCalibKey.Size = new Size(138, 30);
            _btnCalibKey.Click += (s, e) =>
            {
                if (CalibrateRequested != null) CalibrateRequested(CalibrateMode.Single);
            };

            _btnCalibAll.Text = "校准全部按键";
            _btnCalibAll.Location = new Point(154, 42);
            _btnCalibAll.Size = new Size(138, 30);
            _btnCalibAll.Click += (s, e) =>
            {
                if (CalibrateRequested != null) CalibrateRequested(CalibrateMode.All);
            };

            _lblCalibNote.ForeColor = Color.FromArgb(170, 80, 20);
            _lblCalibNote.Location = new Point(10, 78);
            _lblCalibNote.Size = new Size(286, 62);
            _lblCalibNote.Text = "⚠ 校准是阻塞操作：需要「按住 → 松开」，\r\n期间键盘不处理其它指令。\r\n校准完会自动刷新参数。";

            _grp.Controls.Add(_lblCalibTitle);
            _grp.Controls.Add(_btnCalibKey);
            _grp.Controls.Add(_btnCalibAll);
            _grp.Controls.Add(_lblCalibNote);
        }

        // ------------------------------------------------------------
        // ② 写入范围（按键单选 / 多选）
        // ------------------------------------------------------------
        private void BuildBatchPicker()
        {
            _lblBatchTitle.AutoSize = true;
            _lblBatchTitle.Location = new Point(304, 22);
            _lblBatchTitle.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            _lblBatchTitle.Text = "写入范围（左键单选/右键多选）：";

            //按键按钮的大小/间距和左边"选择要修改的按键"保持一致
            _flpBatch.Location = new Point(304, 40);
            _flpBatch.Size = new Size(344, 72);
            _flpBatch.FlowDirection = FlowDirection.LeftToRight;
            _flpBatch.WrapContents = true;

            _btnBatchAll.Text = "全选 / 全不选";
            _btnBatchAll.Location = new Point(304, 116);
            _btnBatchAll.Size = new Size(110, 28);
            _btnBatchAll.FlatStyle = FlatStyle.Flat;
            _btnBatchAll.ForeColor = Color.FromArgb(30, 70, 150);
            _btnBatchAll.UseVisualStyleBackColor = false;
            _btnBatchAll.Click += (s, e) => ToggleAllBatch();

            _btnSyncParams.Text = "重新读取磁轴参数";
            _btnSyncParams.Location = new Point(424, 116);
            _btnSyncParams.Size = new Size(150, 28);
            _btnSyncParams.Click += (s, e) =>
            {
                if (SyncParametersRequested != null) SyncParametersRequested();
            };

            _grp.Controls.Add(_lblBatchTitle);
            _grp.Controls.Add(_flpBatch);
            _grp.Controls.Add(_btnBatchAll);
            _grp.Controls.Add(_btnSyncParams);

            BuildBatchButtons(DeviceLimits.KeyCount);
        }

        private void BuildBatchButtons(int count)
        {
            _flpBatch.Controls.Clear();
            _batchButtons.Clear();
            _selectedKeys.RemoveWhere(k => k >= count);

            for (int i = 0; i < count; i++)
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
                };
                b.MouseDown += BatchButton_MouseDown;
                _flpBatch.Controls.Add(b);
                _batchButtons.Add(b);
            }

            RefreshBatchColors();
        }

        /// <summary>
        /// 和可视化选键一样的手势：**左键 = 单选**（只留这一个），
        /// **右键 / Shift+左键 = 多选切换**。
        /// </summary>
        private void BatchButton_MouseDown(object sender, MouseEventArgs e)
        {
            if (!PickerGesture.IsPick(e)) return;

            int k = (int)((Button)sender).Tag;

            if (PickerGesture.IsAddOnly(e))
            {
                if (!_selectedKeys.Remove(k)) _selectedKeys.Add(k);
            }
            else
            {
                _selectedKeys.Clear();
                _selectedKeys.Add(k);
            }

            RefreshBatchColors();
            UpdateTitle();
            if (SelectionChanged != null) SelectionChanged();
        }

        private void ToggleAllBatch()
        {
            if (_selectedKeys.Count == _batchButtons.Count) _selectedKeys.Clear();
            else
            {
                _selectedKeys.Clear();
                for (int i = 0; i < _batchButtons.Count; i++) _selectedKeys.Add(i);
            }
            RefreshBatchColors();
            UpdateTitle();
            if (SelectionChanged != null) SelectionChanged();
        }

        private void RefreshBatchColors()
        {
            for (int i = 0; i < _batchButtons.Count; i++)
            {
                var b = _batchButtons[i];
                bool sel = _selectedKeys.Contains(i);
                bool dirty = i < _paramDirty.Length && _paramDirty[i];

                //底色只表示"选没选中"：选中=蓝，没选中=白
                //数字颜色表示参数状态：绿 = 和键盘一致，红 = 改过还没写入
                b.BackColor = sel ? Color.FromArgb(150, 200, 255) : Color.White;
                b.ForeColor = dirty ? Color.FromArgb(190, 40, 40) : Color.FromArgb(20, 120, 60);
                b.FlatAppearance.BorderColor = sel ? Color.FromArgb(25, 70, 150) : Color.FromArgb(170, 170, 170);
                b.FlatAppearance.BorderSize = sel ? 2 : 1;
            }
        }

        // ------------------------------------------------------------
        // ③ 四个参数列
        // ------------------------------------------------------------
        private static int ValueToSlider(int i, int value)
        {
            return Invert[i] ? (MinV + MaxV - value) : value;
        }

        private static int SliderToValue(int i, int slider)
        {
            return Invert[i] ? (MinV + MaxV - slider) : slider;
        }

        private void BuildParamColumns()
        {
            for (int i = 0; i < ParamCount; i++)
            {
                int x = ColX0 + i * ColW;
                var opt = BindingCatalog.Parameters[i];

                _lblParamName[i] = new Label
                {
                    Location = new Point(x, 22),
                    Size = new Size(ColW, 16),
                    Text = opt.Name,
                    Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter,
                };

                _lblParamDevice[i] = new Label
                {
                    Location = new Point(x, 38),
                    Size = new Size(ColW, 14),
                    ForeColor = Color.FromArgb(30, 110, 60),
                    Font = new Font("Microsoft YaHei UI", 8f, FontStyle.Regular),
                    Text = "键盘 —",
                    TextAlign = ContentAlignment.MiddleCenter,
                };

                int idx = i;
                _bar[i] = new TrackBar
                {
                    Location = new Point(x + (ColW - 45) / 2, 54),
                    Size = new Size(45, BarH),
                    Orientation = Orientation.Vertical,
                    Minimum = MinV,
                    Maximum = MaxV,
                    TickFrequency = 200,
                    SmallChange = 1,
                    LargeChange = 10,
                    Value = ValueToSlider(i, DeviceLimits.ParamValueDefault),
                };
                _bar[i].ValueChanged += (s, e) => BarChanged(idx);

                _num[i] = new NumericUpDown
                {
                    Location = new Point(x + 4, 154),
                    Size = new Size(ColW - 8, 23),
                    Minimum = MinV,
                    Maximum = MaxV,
                    TextAlign = HorizontalAlignment.Center,
                    Value = DeviceLimits.ParamValueDefault,
                };
                _num[i].ValueChanged += (s, e) => NumChanged(idx);

                _grp.Controls.Add(_lblParamName[i]);
                _grp.Controls.Add(_lblParamDevice[i]);
                _grp.Controls.Add(_bar[i]);
                _grp.Controls.Add(_num[i]);
            }
        }

        private void BarChanged(int i)
        {
            if (_syncInputs) return;
            _syncInputs = true;
            try
            {
                _num[i].Value = SliderToValue(i, _bar[i].Value);
            }
            finally { _syncInputs = false; }

            MarkEdited();
        }

        private void NumChanged(int i)
        {
            if (_syncInputs) return;
            _syncInputs = true;
            try
            {
                _bar[i].Value = ValueToSlider(i, (int)_num[i].Value);
            }
            finally { _syncInputs = false; }

            MarkEdited();
        }

        /// <summary>用户动了参数值 -> 把这次会影响到的按键标红（改过还没写入）。</summary>
        private void MarkEdited()
        {
            foreach (int k in EffectiveKeys())
            {
                if (k >= 0 && k < _paramDirty.Length) _paramDirty[k] = true;
            }
            RefreshBatchColors();
        }

        /// <summary>这次写入实际会作用到哪些按键：勾了就是勾的那些，一个没勾就是当前按键。</summary>
        private List<int> EffectiveKeys()
        {
            var list = new List<int>(_selectedKeys);
            list.Sort();
            if (list.Count == 0) list.Add(_selectedKey);
            return list;
        }

        // ------------------------------------------------------------
        // ④ 写入按钮
        // ------------------------------------------------------------
        private void BuildWriteButton()
        {
            _btnWrite.Text = "将四个参数\r\n发给选中按键";
            _btnWrite.Location = new Point(ColX0 + ParamCount * ColW + 6, 52);
            _btnWrite.Size = new Size(102, 56);
            _btnWrite.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
            _btnWrite.BackColor = Color.FromArgb(210, 232, 255);
            _btnWrite.FlatStyle = FlatStyle.Flat;
            _btnWrite.FlatAppearance.BorderColor = Color.FromArgb(60, 120, 190);
            _btnWrite.ForeColor = Color.FromArgb(15, 60, 130);
            _btnWrite.UseVisualStyleBackColor = false;
            _btnWrite.Click += (s, e) =>
            {
                if (WriteRequested != null) WriteRequested();
            };

            _grp.Controls.Add(_btnWrite);
        }

        // ------------------------------------------------------------
        // 对外接口
        // ------------------------------------------------------------
        /// <summary>主界面当前选中的按键。</summary>
        public int SelectedKey
        {
            get { return _selectedKey; }
            set
            {
                if (_selectedKey == value) return;
                _selectedKey = value;
                UpdateTitle();
            }
        }

        /// <summary>滑块上显示/写入的"焦点按键"：只选了一个就是它，否则跟着主界面。</summary>
        public int FocusKey
        {
            get
            {
                if (_selectedKeys.Count == 1)
                {
                    foreach (int k in _selectedKeys) return k;
                }
                return _selectedKey;
            }
        }

        private void UpdateTitle()
        {
            int shown = _selectedKeys.Count == 1 ? FocusKey : _focusKey;
            _grp.Text = string.Format("磁轴参数 / 校准（当前 #{0}）", shown);
        }

        /// <summary>四个参数的当前输入值（按 BindingCatalog.Parameters 的顺序）。</summary>
        public int[] ParameterValues
        {
            get
            {
                var v = new int[ParamCount];
                for (int i = 0; i < ParamCount; i++) v[i] = (int)_num[i].Value;
                return v;
            }
        }

        /// <summary>这次写入会作用到的按键（空表示只写当前按键）。</summary>
        public List<int> BatchKeys
        {
            get { return EffectiveKeys(); }
        }

        /// <summary>勾了几个按键（用于提示文案）。</summary>
        public int BatchCount { get { return _selectedKeys.Count; } }

        /// <summary>按键数量变了（收到设备信息后）重建按键按钮。</summary>
        public void SetKeyCount(int count)
        {
            BuildBatchButtons(count);
            UpdateTitle();
        }

        /// <summary>连接状态联动：未连接 / 忙时禁用。</summary>
        public void SetEnabledState(bool on)
        {
            _btnWrite.Enabled = on;
            _btnCalibKey.Enabled = on;
            _btnCalibAll.Enabled = on;
            _btnSyncParams.Enabled = on;
            _flpBatch.Enabled = on;
            _btnBatchAll.Enabled = on;
            for (int i = 0; i < ParamCount; i++)
            {
                _bar[i].Enabled = on;
                _num[i].Enabled = on;
            }
        }

        /// <summary>写入成功之后：把参数与键盘重新对齐（红色变回绿色/蓝色）。</summary>
        public void MarkParamsSynced(IEnumerable<int> keys)
        {
            foreach (int k in keys)
            {
                if (k >= 0 && k < _paramDirty.Length) _paramDirty[k] = false;
            }
            RefreshBatchColors();
        }

        /// <summary>全部按键都重新读过一遍参数了。</summary>
        public void MarkAllParamsSynced()
        {
            for (int i = 0; i < _paramDirty.Length; i++) _paramDirty[i] = false;
            RefreshBatchColors();
        }

        /// <summary>
        /// 显示某个按键从键盘查回来的参数值：
        /// 每个滑块上面显示"键盘 xx"，同时把**滑块和值框都填成键盘上的值**，方便在原值上改。
        /// known[i] 为 false 表示这一项还没查到。
        /// </summary>
        public void ShowCurrentValues(int[] values, bool[] known, int key)
        {
            //只更新"显示焦点"，不动 _selectedKey —— 那个是主界面当前按键，不能被这里改掉
            _focusKey = key;
            UpdateTitle();

            _syncInputs = true;
            try
            {
                for (int i = 0; i < ParamCount; i++)
                {
                    bool ok = known != null && i < known.Length && known[i]
                              && values != null && i < values.Length;

                    if (ok)
                    {
                        int v = values[i];
                        if (v < MinV) v = MinV;
                        if (v > MaxV) v = MaxV;

                        _lblParamDevice[i].Text = string.Format("键盘 {0}", v);
                        _lblParamDevice[i].ForeColor = Color.FromArgb(30, 110, 60);
                        _num[i].Value = v;
                        _bar[i].Value = ValueToSlider(i, v);
                    }
                    else
                    {
                        _lblParamDevice[i].Text = "键盘 —";
                        _lblParamDevice[i].ForeColor = Color.FromArgb(150, 150, 150);
                    }
                }
            }
            finally
            {
                _syncInputs = false;
            }
        }
    }
}
