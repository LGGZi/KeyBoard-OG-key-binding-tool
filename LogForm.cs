using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace KeyboardOG
{
    /// <summary>
    /// 指令日志子窗口（纯开发用）。
    ///
    /// 三条硬性要求：
    ///   1) 不能阻塞主窗口 —— 用 Show() 显示，绝不 ShowDialog()；
    ///   2) 不能强制聚焦 —— 重写 ShowWithoutActivation 返回 true，
    ///      并且主窗体重复点击按钮时也不调用 Activate()/BringToFront()；
    ///   3) 只用来查看指令的发送 / 接收状态，所以带一个收发统计条做“接收确认”。
    /// </summary>
    public class LogForm : Form
    {
        private readonly CommandLogView _view = new CommandLogView();
        private readonly CheckBox _chkPause = new CheckBox();
        private readonly CheckBox _chkAutoScroll = new CheckBox();
        private readonly Button _btnClear = new Button();
        private readonly Button _btnExport = new Button();
        private readonly Label _lblHint = new Label();
        private readonly Label _lblSummary = new Label();
        private readonly Timer _timer = new Timer();

        private CommandLogStore _store;

        /// <summary>显示时不抢焦点（WinForms 会为此加上 WS_EX_NOACTIVATE）。</summary>
        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        public LogForm()
        {
            Text = "指令日志 - KeyboardOG";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1180, 560);
            MinimumSize = new Size(760, 320);
            ShowIcon = false;
            ShowInTaskbar = false;
            MinimizeBox = false;
            Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular, GraphicsUnit.Point, 134);
            AutoScaleMode = AutoScaleMode.None;

            var toolbar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.FromArgb(245, 245, 245) };

            _chkPause.Text = "暂停刷新";
            _chkPause.Location = new Point(10, 9);
            _chkPause.AutoSize = true;
            _chkPause.CheckedChanged += (s, e) => { _view.Paused = _chkPause.Checked; };

            _chkAutoScroll.Text = "自动滚动";
            _chkAutoScroll.Location = new Point(96, 9);
            _chkAutoScroll.AutoSize = true;
            _chkAutoScroll.Checked = true;
            _chkAutoScroll.CheckedChanged += (s, e) => { _view.AutoScrollToEnd = _chkAutoScroll.Checked; };

            _btnClear.Text = "清空日志";
            _btnClear.Location = new Point(192, 6);
            _btnClear.Size = new Size(84, 26);
            _btnClear.Click += (s, e) =>
            {
                if (_store != null) _store.Clear();
                _view.ClearView();
            };

            _btnExport.Text = "导出 CSV";
            _btnExport.Location = new Point(282, 6);
            _btnExport.Size = new Size(84, 26);
            _btnExport.Click += OnExport;

            _lblHint.Text = "关闭本窗口只是隐藏，不会影响主窗口。";
            _lblHint.Location = new Point(380, 11);
            _lblHint.AutoSize = true;
            _lblHint.ForeColor = Color.DimGray;

            toolbar.Controls.Add(_chkPause);
            toolbar.Controls.Add(_chkAutoScroll);
            toolbar.Controls.Add(_btnClear);
            toolbar.Controls.Add(_btnExport);
            toolbar.Controls.Add(_lblHint);

            // 收发确认条：一眼看出“指令发出去了几条、键盘回了几条”。
            _lblSummary.Dock = DockStyle.Bottom;
            _lblSummary.Height = 26;
            _lblSummary.TextAlign = ContentAlignment.MiddleLeft;
            _lblSummary.Padding = new Padding(10, 0, 0, 0);
            _lblSummary.BackColor = Color.FromArgb(250, 250, 250);
            _lblSummary.ForeColor = Color.FromArgb(60, 60, 60);

            _view.Dock = DockStyle.Fill;
            Controls.Add(_view);
            Controls.Add(_lblSummary);
            Controls.Add(toolbar);

            _timer.Interval = 500;
            _timer.Tick += (s, e) => UpdateSummary();
            _timer.Start();
        }

        public void Attach(CommandLogStore store)
        {
            _store = store;
            _view.Attach(store);
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            if (_store == null) return;

            var all = _store.Snapshot();
            int tx = all.Count(e => e.Kind == LogKind.Tx);
            int rx = all.Count(e => e.Kind == LogKind.Rx);
            int done = all.Count(e => e.Kind == LogKind.Tx && e.State == LogState.Done);
            int timeout = all.Count(e => e.Kind == LogKind.Tx && e.State == LogState.Timeout);
            int failed = all.Count(e => e.Kind == LogKind.Tx && e.State == LogState.Failed);

            _lblSummary.Text = string.Format(
                "共 {0} 条   ·   发送 {1}   接收确认 {2}   已完成 {3}   超时 {4}   失败 {5}",
                all.Count, tx, rx, done, timeout, failed);

            Text = string.Format("指令日志 - KeyboardOG   （发送 {0} / 接收 {1}）", tx, rx);
        }

        /// <summary>挑一个肯定能写的地方当默认目录（exe 可能在 Program Files 里，没写权限）。</summary>
        private static string SafeFolder()
        {
            string dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) return dir;

            dir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) return dir;

            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private void OnExport(object sender, EventArgs e)
        {
            string safeDir = SafeFolder();

            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*";
                dlg.DefaultExt = "csv";
                dlg.AddExtension = true;
                dlg.OverwritePrompt = true;
                dlg.FileName = "keyboardog_log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
                dlg.InitialDirectory = safeDir;      //别默认落在 exe 目录（可能是 Program Files）
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                string path = dlg.FileName;
                if (!Path.HasExtension(path)) path += ".csv";

                try
                {
                    _view.ExportCsv(path);
                    MessageBox.Show(this, "已导出：\r\n" + path, "导出成功",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    //那个位置没有写权限 -> 退到"文档"里再存一次，至少别把日志丢了
                    try
                    {
                        string fallback = Path.Combine(safeDir, Path.GetFileName(path));
                        _view.ExportCsv(fallback);

                        MessageBox.Show(this,
                            "那个位置没有写入权限：\r\n" + path +
                            "\r\n\r\n已经改存到「文档」里了：\r\n" + fallback,
                            "导出成功（换了位置）", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    catch (Exception ex2)
                    {
                        MessageBox.Show(this,
                            "导出失败：没有写入权限。\r\n\r\n原路径：" + path +
                            "\r\n\r\n换个有权限的目录（例如「文档」）再试。\r\n\r\n" + ex2.Message,
                            "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "导出失败：" + ex.Message, "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Stop();
                _timer.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 用户点右上角关闭时只隐藏，下次打开还能看到历史日志。
            // 主窗口关闭（FormOwnerClosing）时才真正销毁。
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }
    }
}
