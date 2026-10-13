using System;
using System.Drawing;
using System.Windows.Forms;

namespace KeyboardOG
{
    /// <summary>
    /// 参数设置子窗口。
    ///
    /// 按需求做成**模态**（ShowDialog）：独占聚焦、始终在主界面前方，
    /// 不点确定/取消就回不到主界面，避免设置改到一半就去操作设备。
    ///
    /// 「取消」= 完全不生效（在副本上改，只有确定才写回去并保存文件）。
    /// </summary>
    public class SettingsForm : Form
    {
        private readonly NumericUpDown _numRate = new NumericUpDown();
        private readonly NumericUpDown _numTimeout = new NumericUpDown();
        private readonly CheckBox _chkWaitFin = new CheckBox();
        private readonly Label _lblGap = new Label();
        private readonly Label _lblPath = new Label();
        private readonly Button _btnOk = new Button();
        private readonly Button _btnCancel = new Button();
        private readonly Button _btnDefault = new Button();

        private readonly AppSettings _working;

        /// <summary>确定之后的结果；取消时这个值不会被使用。</summary>
        public AppSettings Result { get { return _working; } }

        public SettingsForm(AppSettings current)
        {
            _working = current.Clone();

            Text = "参数设置 - KeyboardOG";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(520, 306);
            Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular, GraphicsUnit.Point, 134);
            AutoScaleMode = AutoScaleMode.None;

            var grp = new GroupBox
            {
                Text = "发送与等待",
                Location = new Point(12, 8),
                Size = new Size(496, 148),
            };

            var lbl1 = new Label { Text = "发送频率：", AutoSize = true, Location = new Point(16, 32) };
            _numRate.Location = new Point(100, 28);
            _numRate.Size = new Size(84, 25);
            _numRate.TextAlign = HorizontalAlignment.Right;
            _numRate.Minimum = AppSettings.MinSendRateHz;
            _numRate.Maximum = AppSettings.MaxSendRateHz;
            _numRate.Value = _working.SendRateHz;
            _numRate.ValueChanged += (s, e) => UpdateGapLabel();

            var lbl2 = new Label { Text = "Hz", AutoSize = true, Location = new Point(192, 32) };
            _lblGap.AutoSize = true;
            _lblGap.ForeColor = Color.FromArgb(20, 70, 150);
            _lblGap.Location = new Point(222, 32);

            var lbl3 = new Label { Text = "回应超时：", AutoSize = true, Location = new Point(16, 70) };
            _numTimeout.Location = new Point(100, 66);
            _numTimeout.Size = new Size(84, 25);
            _numTimeout.TextAlign = HorizontalAlignment.Right;
            _numTimeout.Minimum = AppSettings.MinTimeoutMs;
            _numTimeout.Maximum = AppSettings.MaxTimeoutMs;
            _numTimeout.Value = _working.ResponseTimeoutMs;

            var lbl4 = new Label { Text = "ms", AutoSize = true, Location = new Point(192, 70) };

            _chkWaitFin.Text = "每条指令等键盘回 FIN 之后再发下一条（建议开启）";
            _chkWaitFin.AutoSize = true;
            _chkWaitFin.Location = new Point(16, 106);
            _chkWaitFin.Checked = _working.WaitForFin;

            grp.Controls.Add(lbl1);
            grp.Controls.Add(_numRate);
            grp.Controls.Add(lbl2);
            grp.Controls.Add(_lblGap);
            grp.Controls.Add(lbl3);
            grp.Controls.Add(_numTimeout);
            grp.Controls.Add(lbl4);
            grp.Controls.Add(_chkWaitFin);

            var note = new Label
            {
                ForeColor = Color.DimGray,
                Location = new Point(12, 162),
                Size = new Size(496, 78),
                Text = "说明：\r\n" +
                       "· 发送频率 = 批量写入时两条指令之间的最小间隔，建议 500Hz（约 2ms 一条）。\r\n" +
                       "· 回应超时 = 等 ACK / FIN 的最长时间，超时会在日志里标成橙色“超时”。\r\n" +
                       "· 关掉“等 FIN”会连续快发，容易丢指令，建议保持开启。",
            };

            _lblPath.ForeColor = Color.FromArgb(110, 110, 115);
            _lblPath.Location = new Point(12, 240);
            _lblPath.Size = new Size(496, 20);
            _lblPath.Text = "保存位置：" + AppSettings.FilePath;

            _btnOk.Text = "确定";
            _btnOk.Location = new Point(268, 266);
            _btnOk.Size = new Size(76, 28);
            _btnOk.DialogResult = DialogResult.OK;
            _btnOk.Click += (s, e) => ApplyFromControls();

            _btnCancel.Text = "取消";
            _btnCancel.Location = new Point(350, 266);
            _btnCancel.Size = new Size(76, 28);
            _btnCancel.DialogResult = DialogResult.Cancel;

            _btnDefault.Text = "恢复默认";
            _btnDefault.Location = new Point(432, 266);
            _btnDefault.Size = new Size(76, 28);
            _btnDefault.Click += (s, e) =>
            {
                _working.ResetToDefault();
                _numRate.Value = _working.SendRateHz;
                _numTimeout.Value = _working.ResponseTimeoutMs;
                _chkWaitFin.Checked = _working.WaitForFin;
                UpdateGapLabel();
            };

            Controls.Add(grp);
            Controls.Add(note);
            Controls.Add(_lblPath);
            Controls.Add(_btnOk);
            Controls.Add(_btnCancel);
            Controls.Add(_btnDefault);

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;

            UpdateGapLabel();
        }

        private void ApplyFromControls()
        {
            _working.SendRateHz = (int)_numRate.Value;
            _working.ResponseTimeoutMs = (int)_numTimeout.Value;
            _working.WaitForFin = _chkWaitFin.Checked;
            _working.Clamp();
        }

        private void UpdateGapLabel()
        {
            int hz = (int)_numRate.Value;
            int ms = Math.Max(1, (int)Math.Round(1000.0 / Math.Max(1, hz)));
            _lblGap.Text = string.Format("≈ {0} ms / 条（约 {1} 条/秒）", ms, 1000 / ms);
        }
    }
}
