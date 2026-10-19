using System;
using System.Drawing;
using System.Windows.Forms;

namespace KeyboardOG
{
    /// <summary>
    /// 独立的按键布局窗口：内容与主界面左侧的布局面板一致（同一个 LayoutPanel）。
    /// 按之前的约定：不记忆位置和尺寸、不置顶，关闭即释放，下次打开重新居中。
    /// </summary>
    public sealed class KeyLayoutForm : Form
    {
        public readonly LayoutPanel Panel;

        /// <summary>窗口里点了某个按键（转给主窗体去切换当前按键）。</summary>
        public event Action<int> KeyClicked;

        /// <summary>把窗口尺寸贴到布局控件上（窗口比控件大出一圈会让布局看着很空）。</summary>
        public void FitToContent()
        {
            Panel.MaxPixelsPerUnit = 52;
            var s = Panel.PreferredContentSize(900, 700);
            if (s.Width <= 0) return;
            ClientSize = new Size(s.Width + 28, s.Height + 28);
        }

        public KeyLayoutForm()
        {
            Text = L.T("按键布局", "Key layout");
            ClientSize = new Size(600, 560);
            MinimumSize = new Size(320, 260);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            Font = new Font("Microsoft YaHei UI", 9f);
            BackColor = Color.FromArgb(246, 247, 249);

            Panel = new LayoutPanel();
            Panel.Dock = DockStyle.Fill;
            Panel.Margin = new Padding(6);
            Panel.KeySelected += i =>
            {
                if (KeyClicked != null) KeyClicked(i);
            };

            var host = new Panel();
            host.Dock = DockStyle.Fill;
            host.Padding = new Padding(6);
            host.Controls.Add(Panel);
            Controls.Add(host);
        }
    }
}
