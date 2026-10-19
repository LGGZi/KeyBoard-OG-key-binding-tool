using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace KLEconvert
{
    public class Form1 : Form
    {
        private KleLayout _layout;
        private KeyItem _selected;
        private List<KeyItem> _listOrder = new List<KeyItem>();
        private bool _syncing;
        private string _sourcePath = "";

        private LayoutCanvas _canvas;
        private ToolStrip _tools;
        private ToolStripButton _btnOpen, _btnAuto, _btnClear, _btnExportJson, _btnExportH;
        private Panel _left;
        private NumericUpDown _numIndex, _numX, _numY, _numW, _numH;
        private CheckBox _chkNone;
        private TextBox _txtLabel;
        private ComboBox _cboType;
        private ListView _lv;
        private StatusStrip _status;
        private ToolStripStatusLabel _lblStatus;

        public Form1()
        {
            Text = "KLEconvert — KLE 配列查看 / 编号 / 导出";
            ClientSize = new Size(1180, 760);
            MinimumSize = new Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;
            AllowDrop = true;
            Font = new Font("Microsoft YaHei UI", 9f);

            BuildUi();
            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            UpdateStatus();
        }

        // ------------------------------------------------------------ UI
        private void BuildUi()
        {
            _canvas = new LayoutCanvas();
            _canvas.Dock = DockStyle.Fill;
            _canvas.SelectionChanged += delegate { OnCanvasSelectionChanged(); };
            _canvas.DragEnter += OnDragEnter;
            _canvas.DragDrop += OnDragDrop;
            Controls.Add(_canvas);

            _left = new Panel();
            _left.Dock = DockStyle.Left;
            _left.Width = 336;
            _left.AutoScroll = true;
            _left.BackColor = Color.FromArgb(246, 247, 249);
            _left.Padding = new Padding(8);
            Controls.Add(_left);

            _tools = new ToolStrip();
            _tools.GripStyle = ToolStripGripStyle.Hidden;
            _tools.RenderMode = ToolStripRenderMode.System;

            _btnOpen = new ToolStripButton("打开 JSON…");
            _btnOpen.Click += delegate { OpenJson(); };
            _btnAuto = new ToolStripButton("一键编号");
            _btnAuto.Click += delegate { AutoNumber(); };
            _btnClear = new ToolStripButton("清除编号");
            _btnClear.Click += delegate { ClearNumbers(); };
            _btnExportJson = new ToolStripButton("导出 JSON…");
            _btnExportJson.Click += delegate { ExportJson(); };
            _btnExportH = new ToolStripButton("导出 .h…");
            _btnExportH.Click += delegate { ExportHeader(); };

            _tools.Items.Add(_btnOpen);
            _tools.Items.Add(new ToolStripSeparator());
            _tools.Items.Add(_btnAuto);
            _tools.Items.Add(_btnClear);
            _tools.Items.Add(new ToolStripSeparator());
            _tools.Items.Add(_btnExportJson);
            _tools.Items.Add(_btnExportH);
            Controls.Add(_tools);

            _status = new StatusStrip();
            _lblStatus = new ToolStripStatusLabel("");
            _lblStatus.Spring = true;
            _lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            _status.Items.Add(_lblStatus);
            Controls.Add(_status);

            BuildLeftPanel();
        }

        private void BuildLeftPanel()
        {
            var gbSel = new GroupBox();
            gbSel.Text = "选中按键";
            gbSel.Location = new Point(8, 8);
            gbSel.Size = new Size(310, 268);
            _left.Controls.Add(gbSel);

            AddLabel(gbSel, "编号", 12, 30, 40);
            _numIndex = new NumericUpDown();
            _numIndex.Location = new Point(56, 27);
            _numIndex.Size = new Size(74, 24);
            _numIndex.Minimum = 0;
            _numIndex.Maximum = 255;
            _numIndex.TextAlign = HorizontalAlignment.Center;
            _numIndex.ValueChanged += delegate { OnIndexChanged(); };
            _numIndex.KeyDown += OnIndexKeyDown;
            gbSel.Controls.Add(_numIndex);

            _chkNone = new CheckBox();
            _chkNone.Text = "无编号";
            _chkNone.Location = new Point(144, 28);
            _chkNone.Size = new Size(84, 22);
            _chkNone.CheckedChanged += delegate { OnNoneChanged(); };
            gbSel.Controls.Add(_chkNone);

            AddLabel(gbSel, "类型", 12, 64, 40);
            _cboType = new ComboBox();
            _cboType.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboType.Location = new Point(56, 61);
            _cboType.Size = new Size(172, 24);
            _cboType.Items.AddRange(new object[] { "0 普通", "1 修饰键", "2 编码器", "3 装饰" });
            _cboType.SelectedIndexChanged += delegate { OnTypeChanged(); };
            gbSel.Controls.Add(_cboType);

            AddLabel(gbSel, "标签", 12, 98, 40);
            _txtLabel = new TextBox();
            _txtLabel.Location = new Point(56, 95);
            _txtLabel.Size = new Size(172, 24);
            _txtLabel.TextChanged += delegate { OnLabelChanged(); };
            gbSel.Controls.Add(_txtLabel);

            AddLabel(gbSel, "坐标 / 尺寸（单位 0.25u，4 = 1u）", 12, 128, 270, Color.DimGray);

            AddLabel(gbSel, "x", 12, 152, 16);
            _numX = AddSpin(gbSel, 30, 149, 62);
            AddLabel(gbSel, "y", 100, 152, 16);
            _numY = AddSpin(gbSel, 118, 149, 62);
            AddLabel(gbSel, "w", 188, 152, 16);
            _numW = AddSpin(gbSel, 206, 149, 62);

            AddLabel(gbSel, "h", 12, 182, 16);
            _numH = AddSpin(gbSel, 30, 179, 62);

            var hint = new Label();
            hint.Text = "点击右侧按键 → 直接敲数字改编号\nEnter 跳到下一个；滚轮缩放画布\n拖入 .json 可载入新配列";
            hint.Location = new Point(12, 212);
            hint.Size = new Size(286, 50);
            hint.ForeColor = Color.DimGray;
            gbSel.Controls.Add(hint);

            var gbList = new GroupBox();
            gbList.Text = "全部按键（按位置排序）";
            gbList.Location = new Point(8, 282);
            gbList.Size = new Size(310, 420);
            gbList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom;
            _left.Controls.Add(gbList);

            _lv = new ListView();
            _lv.View = View.Details;
            _lv.FullRowSelect = true;
            _lv.HideSelection = false;
            _lv.Location = new Point(8, 20);
            _lv.Size = new Size(294, 390);
            _lv.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _lv.Columns.Add("#", 40);
            _lv.Columns.Add("编号", 52);
            _lv.Columns.Add("x", 52);
            _lv.Columns.Add("y", 52);
            _lv.Columns.Add("w", 52);
            _lv.Columns.Add("h", 40);
            _lv.SelectedIndexChanged += delegate { OnListSelectionChanged(); };
            gbList.Controls.Add(_lv);
        }

        private static void AddLabel(Control parent, string text, int x, int y, int w)
        {
            AddLabel(parent, text, x, y, w, Color.Empty);
        }

        private static void AddLabel(Control parent, string text, int x, int y, int w, Color color)
        {
            var l = new Label();
            l.Text = text;
            l.Location = new Point(x, y);
            l.Size = new Size(w, 20);
            if (color != Color.Empty) l.ForeColor = color;
            parent.Controls.Add(l);
        }

        private NumericUpDown AddSpin(Control parent, int x, int y, int w)
        {
            var n = new NumericUpDown();
            n.Location = new Point(x, y);
            n.Size = new Size(w, 24);
            n.Minimum = 0;
            n.Maximum = 255;
            n.TextAlign = HorizontalAlignment.Center;
            n.ValueChanged += delegate { OnGeometryChanged(); };
            parent.Controls.Add(n);
            return n;
        }

        // ------------------------------------------------------------ load
        /// <summary>供命令行/拖入调用：直接载入一个 KLE json。</summary>
        public void LoadPath(string path)
        {
            LoadFile(path);
        }

        private void OpenJson()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "KLE JSON (*.json)|*.json|所有文件 (*.*)|*.*";
                dlg.Title = "打开 KLE Raw data";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                LoadFile(dlg.FileName);
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
            else e.Effect = DragDropEffects.None;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0) return;
            LoadFile(files[0]);
        }

        private void LoadFile(string path)
        {
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                var layout = KleLayout.Parse(json);
                layout.SourceName = Path.GetFileName(path);
                _layout = layout;
                _sourcePath = path;
                _selected = null;
                _canvas.Layout = layout;
                _canvas.Selected = null;
                RebuildList();
                if (_listOrder.Count > 0) _canvas.Selected = _listOrder[0];
                Text = "KLEconvert — " + Path.GetFileName(path);
                UpdateStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法解析这个 json：\r\n" + ex.Message, "打开失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ------------------------------------------------------------ actions
        /// <summary>供截图/自检模式调用。</summary>
        public void ApplyAutoNumberForShot()
        {
            AutoNumber();
        }

        private void AutoNumber()
        {
            if (_layout == null) { NoLayout(); return; }
            _layout.AutoNumber();
            SyncEditorFromSelection();
            RebuildList();
            _canvas.Invalidate();
            UpdateStatus("已按「从上到下、从左到右」编号 0…" + (_layout.Keys.Count - 1));
        }

        private void ClearNumbers()
        {
            if (_layout == null) { NoLayout(); return; }
            _layout.ClearNumbers();
            SyncEditorFromSelection();
            RebuildList();
            _canvas.Invalidate();
            UpdateStatus("已清除全部编号");
        }

        private void ExportJson()
        {
            if (_layout == null) { NoLayout(); return; }
            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "KLE JSON (*.json)|*.json";
                dlg.FileName = SuggestName(false);
                dlg.Title = "导出带编号的 KLE JSON";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                File.WriteAllText(dlg.FileName, _layout.ToJson(true), new UTF8Encoding(false));
                UpdateStatus("已导出 JSON：" + Path.GetFileName(dlg.FileName));
            }
        }

        private void ExportHeader()
        {
            if (_layout == null) { NoLayout(); return; }
            int unnumbered = _layout.Keys.Count - _layout.NumberedCount;
            if (unnumbered > 0)
            {
                var r = MessageBox.Show(this,
                    "还有 " + unnumbered + " 个键没有编号。\r\n\r\n导出 .h 将按位置顺序输出（与 kle2c 的位置模式一致）。\r\n继续吗？",
                    "提示", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                if (r != DialogResult.OK) return;
            }

            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "C 头文件 (*.h)|*.h";
                dlg.FileName = SuggestName(true);
                dlg.Title = "导出固件布局表";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                File.WriteAllText(dlg.FileName, _layout.ToHeader("layout_custom", true), new UTF8Encoding(false));
                UpdateStatus("已导出 .h：" + Path.GetFileName(dlg.FileName));
            }
        }

        private string SuggestName(bool header)
        {
            string baseName = string.IsNullOrEmpty(_sourcePath) ? "layout" : Path.GetFileNameWithoutExtension(_sourcePath);
            return baseName + (header ? ".h" : ".numbered.json");
        }

        private void NoLayout()
        {
            MessageBox.Show(this, "先打开或拖入一个 KLE 导出的 .json。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ------------------------------------------------------------ selection / editing
        private void OnCanvasSelectionChanged()
        {
            _selected = _canvas.Selected;
            SyncEditorFromSelection();
            SyncListSelection();
            UpdateStatus();
        }

        private void OnListSelectionChanged()
        {
            if (_syncing || _layout == null) return;
            if (_lv.SelectedIndices.Count == 0) return;
            int i = _lv.SelectedIndices[0];
            if (i < 0 || i >= _listOrder.Count) return;
            _canvas.Selected = _listOrder[i];
        }

        private void SyncEditorFromSelection()
        {
            _syncing = true;
            try
            {
                bool has = _selected != null;
                _numIndex.Enabled = has;
                _chkNone.Enabled = has;
                _cboType.Enabled = has;
                _numX.Enabled = _numY.Enabled = _numW.Enabled = _numH.Enabled = has;
                _txtLabel.Enabled = has;

                if (!has) return;

                _txtLabel.Text = _selected.Name;

                _chkNone.Checked = _selected.Index < 0;
                _numIndex.Value = _selected.Index >= 0 ? Math.Min(255, _selected.Index) : 0;
                _cboType.SelectedIndex = Math.Min(3, Math.Max(0, _selected.Type));
                _numX.Value = Math.Min(255, _selected.Ux);
                _numY.Value = Math.Min(255, _selected.Uy);
                _numW.Value = Math.Min(255, Math.Max(1, _selected.Uw));
                _numH.Value = Math.Min(255, Math.Max(1, _selected.Uh));
            }
            finally
            {
                _syncing = false;
            }

            if (_selected != null)
            {
                _numIndex.Focus();
                _numIndex.Select(0, _numIndex.Text.Length);
            }
        }

        private void OnIndexChanged()
        {
            if (_syncing || _selected == null) return;
            _selected.Index = (int)_numIndex.Value;
            if (_chkNone.Checked)
            {
                _syncing = true;
                _chkNone.Checked = false;
                _syncing = false;
            }
            RefreshKey(_selected);
        }

        private void OnNoneChanged()
        {
            if (_syncing || _selected == null) return;
            if (_chkNone.Checked)
            {
                _selected.Index = -1;
            }
            else
            {
                _selected.Index = (int)_numIndex.Value;
            }
            RefreshKey(_selected);
        }

        private void OnLabelChanged()
        {
            if (_syncing || _selected == null) return;
            var lines = new List<string>();
            foreach (string l in _selected.Labels)
                if (KeyItem.IsNumberLine(l)) lines.Add(l);
            string text = _txtLabel.Text;
            if (text.Trim().Length > 0) lines.Add(text);
            if (lines.Count == 0) lines.Add(string.Empty);
            _selected.Labels = lines;
            _canvas.Invalidate();
            UpdateStatus();
        }

        private void OnTypeChanged()
        {
            if (_syncing || _selected == null) return;
            _selected.Type = Math.Max(0, _cboType.SelectedIndex);
            _canvas.Invalidate();
            UpdateListRow(_selected);
        }

        private void OnGeometryChanged()
        {
            if (_syncing || _selected == null) return;
            _selected.X = (double)_numX.Value / 4.0;
            _selected.Y = (double)_numY.Value / 4.0;
            _selected.W = Math.Max(0.25, (double)_numW.Value / 4.0);
            _selected.H = Math.Max(0.25, (double)_numH.Value / 4.0);
            _layout.Measure();
            _canvas.Invalidate();
            UpdateListRow(_selected);
            UpdateStatus();
        }

        private void OnIndexKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            if (_selected != null) _selected.Index = (int)_numIndex.Value;
            RefreshKey(_selected);
            SelectNext();
        }

        private void SelectNext()
        {
            if (_layout == null || _listOrder.Count == 0) return;
            int i = _selected == null ? -1 : _listOrder.IndexOf(_selected);
            int next = (i + 1) % _listOrder.Count;
            _canvas.Selected = _listOrder[next];
        }

        // ------------------------------------------------------------ list / status
        private void RebuildList()
        {
            _syncing = true;
            try
            {
                _lv.BeginUpdate();
                _lv.Items.Clear();
                _listOrder = new List<KeyItem>(_layout == null ? new KeyItem[0] : _layout.Keys.ToArray());
                _listOrder.Sort(delegate (KeyItem a, KeyItem b)
                {
                    int c = a.Uy.CompareTo(b.Uy);
                    if (c != 0) return c;
                    return a.Ux.CompareTo(b.Ux);
                });

                foreach (var k in _listOrder)
                {
                    var item = new ListViewItem((_lv.Items.Count + 1).ToString(CultureInfo.InvariantCulture));
                    item.SubItems.Add(k.Index >= 0 ? k.Index.ToString(CultureInfo.InvariantCulture) : "—");
                    item.SubItems.Add(k.Ux.ToString(CultureInfo.InvariantCulture));
                    item.SubItems.Add(k.Uy.ToString(CultureInfo.InvariantCulture));
                    item.SubItems.Add(k.Uw.ToString(CultureInfo.InvariantCulture));
                    item.SubItems.Add(k.Uh.ToString(CultureInfo.InvariantCulture));
                    item.Tag = k;
                    _lv.Items.Add(item);
                }
                _lv.EndUpdate();
            }
            finally
            {
                _syncing = false;
            }
            SyncListSelection();
        }

        private void SyncListSelection()
        {
            if (_layout == null || _selected == null) return;
            int i = _listOrder.IndexOf(_selected);
            if (i < 0 || i >= _lv.Items.Count) return;
            _syncing = true;
            try
            {
                _lv.SelectedIndices.Clear();
                _lv.Items[i].Selected = true;
                _lv.Items[i].Focused = true;
                _lv.EnsureVisible(i);
            }
            finally
            {
                _syncing = false;
            }
        }

        private void RefreshKey(KeyItem k)
        {
            _canvas.Invalidate();
            if (k != null) UpdateListRow(k);
            UpdateStatus();
        }

        private void UpdateListRow(KeyItem k)
        {
            int i = _listOrder.IndexOf(k);
            if (i < 0 || i >= _lv.Items.Count) return;
            var item = _lv.Items[i];
            item.SubItems[1].Text = k.Index >= 0 ? k.Index.ToString(CultureInfo.InvariantCulture) : "—";
            item.SubItems[2].Text = k.Ux.ToString(CultureInfo.InvariantCulture);
            item.SubItems[3].Text = k.Uy.ToString(CultureInfo.InvariantCulture);
            item.SubItems[4].Text = k.Uw.ToString(CultureInfo.InvariantCulture);
            item.SubItems[5].Text = k.Uh.ToString(CultureInfo.InvariantCulture);
        }

        private void UpdateStatus()
        {
            UpdateStatus(null);
        }

        private void UpdateStatus(string extra)
        {
            if (_layout == null)
            {
                _lblStatus.Text = "拖入 KLE 导出的 .json，或点「打开 JSON…」";
                return;
            }
            string text = string.Format(CultureInfo.InvariantCulture,
                "{0} 键 · {1:0.00}u × {2:0.00}u · 已编号 {3}/{4}",
                _layout.Keys.Count, _layout.WidthU, _layout.HeightU,
                _layout.NumberedCount, _layout.Keys.Count);
            if (_layout.Warnings.Count > 0) text += " · 警告 " + _layout.Warnings.Count;
            if (_selected != null) text += " · 选中 #" + (_selected.Index >= 0 ? _selected.Index.ToString(CultureInfo.InvariantCulture) : "—");
            if (!string.IsNullOrEmpty(extra)) text = extra + " · " + text;
            _lblStatus.Text = text;
        }
    }
}
