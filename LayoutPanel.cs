using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace KeyboardOG
{
    /// <summary>
    /// 右侧布局面板：按固件上传的布局绘制按键。
    ///
    /// · 点击按键 → 抛出 KeySelected(index)，主窗体用它切换当前按键
    /// · 颜色与左上角的按键按钮一致：绿 = 与键盘一致，红 = 本地改过，蓝框 = 当前选中
    /// · 没有布局（旧固件 / 查询失败）时显示占位提示，回退到固定网格
    /// </summary>
    public sealed class LayoutPanel : Control
    {
        private KeyLayout _layout;
        private bool[] _dirty = new bool[0];
        private string[] _texts = new string[0];
        private int _current = -1;
        private int _keyCount = -1;
        private string _emptyText = L.T("无布局信息", "No layout");
        private float _zoom = 1.0f;

        /// <summary>1u 允许的最大像素；&lt;=0 表示不限制（独立窗口按窗口大小铺满）。</summary>
        public int MaxPixelsPerUnit = 44;

        /// <summary>点击了某个按键（参数是按键编号）。</summary>
        public event Action<int> KeySelected;

        /// <summary>双击了某个按键（用来打开独立窗口）。</summary>
        public event Action<int> KeyDoubleClicked;

        public LayoutPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.FromArgb(250, 250, 252);
            Font = new Font("Microsoft YaHei UI", 9f);
        }

        /// <summary>设置可选的按键数量（设备信息里的 BUTTONCOUNT，夹到本地上限）。</summary>
        public void SetKeyCount(int count)
        {
            if (count < 0) count = 0;
            if (_keyCount == count) return;
            _keyCount = count;
            _dirty = new bool[count];
            _texts = new string[count];
            if (_layout != null && _layout.Count != count)
            {
                //布局和当前按键数对不上：作废，回退固定网格
                _layout = null;
                _emptyText = L.T("布局与按键数不一致", "Layout does not match key count");
            }
            Invalidate();
        }

        /// <summary>应用一份校验通过的布局；传 null 表示没有布局。</summary>
        public void SetLayout(KeyLayout layout)
        {
            _layout = layout;
            if (layout != null) _zoom = 1.0f;
            Invalidate();
        }

        public void SetEmpty(string text)
        {
            _layout = null;
            if (!string.IsNullOrEmpty(text)) _emptyText = text;
            Invalidate();
        }

        public new KeyLayout Layout { get { return _layout; } }

        /// <summary>更新某个按键的颜色状态。</summary>
        /// <summary>设置键帽上显示的文字（映射摘要）；传空表示只显示编号。</summary>
        public void SetKeyText(int index, string text)
        {
            if (index < 0 || index >= _texts.Length) return;
            if (_texts[index] == text) return;
            _texts[index] = text;
            Invalidate();
        }

        public void SetKeyState(int index, bool dirty, bool current)
        {
            if (index < 0) return;
            if (index < _dirty.Length) _dirty[index] = dirty;
            if (current) _current = index;
            else if (_current == index) _current = -1;
            Invalidate();
        }

        public void SetCurrent(int index)
        {
            _current = index;
            Invalidate();
        }

        // ------------------------------------------------------------ 绘制
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            if (_layout == null || _layout.Count == 0)
            {
                TextRenderer.DrawText(g, _emptyText, Font, ClientRectangle,
                    Color.FromArgb(150, 150, 158),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
                return;
            }

            float ppu = PixelsPerUnit();
            float w = _layout.WidthU * ppu;
            float h = _layout.HeightU * ppu;
            float ox = Math.Max(8f, (ClientSize.Width - w) / 2f);
            float oy = Math.Max(8f, (ClientSize.Height - h) / 2f);

            using (var fontIndex = new Font(Font.FontFamily, Math.Max(7f, Math.Min(12f, ppu * 0.22f)), FontStyle.Bold))
            using (var fontSmall = new Font(Font.FontFamily, Math.Max(6f, Math.Min(10f, ppu * 0.15f))))
            using (var fontMain = new Font(Font.FontFamily, Math.Max(5f, Math.Min(8f, ppu * 0.12f)), FontStyle.Regular))
            using (var fontMain2 = new Font(Font.FontFamily, Math.Max(4.5f, Math.Min(7f, ppu * 0.10f)), FontStyle.Regular))
            using (var fontMain3 = new Font(Font.FontFamily, Math.Max(4f, Math.Min(6f, ppu * 0.085f)), FontStyle.Regular))
            {
                foreach (var k in _layout.Keys)
                {
                    var rect = new RectangleF(ox + k.UnitsX * ppu, oy + k.UnitsY * ppu,
                                              k.UnitsW * ppu - 2f, k.UnitsH * ppu - 2f);
                    DrawKey(g, rect, k, fontIndex, fontSmall, fontMain, fontMain2, fontMain3);
                }
            }
        }

        private float PixelsPerUnit()
        {
            float sx = (ClientSize.Width - 24f) / _layout.WidthU;
            float sy = (ClientSize.Height - 24f) / _layout.HeightU;
            float ppu = Math.Min(sx, sy);
            if (MaxPixelsPerUnit > 0 && ppu > MaxPixelsPerUnit) ppu = MaxPixelsPerUnit;
            if (ppu < 6f) ppu = 6f;
            if (ppu > 96f) ppu = 96f;
            return ppu * _zoom;
        }

        private static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2f;
            if (d <= 0) { path.AddRectangle(r); return path; }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void DrawKey(Graphics g, RectangleF r, KeyLayoutItem k, Font fontIndex, Font fontSmall, Font fontMain, Font fontMain2, Font fontMain3)
        {
            bool selected = k.Index == _current;
            bool dirty = k.Index >= 0 && k.Index < _dirty.Length && _dirty[k.Index];

            Color fill = selected ? Color.FromArgb(198, 222, 255) : Color.White;
            Color border = selected ? Color.FromArgb(40, 100, 200) : Color.FromArgb(170, 175, 185);
            Color text = dirty ? Color.FromArgb(190, 40, 40) : Color.FromArgb(20, 120, 60);

            using (var path = RoundRect(r, Math.Min(6f, r.Height * 0.15f)))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border, selected ? 2f : 1f))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }

            // 编号固定画在左上角（小字）
            string title = k.Index.ToString(CultureInfo.InvariantCulture);
            var numRect = new Rectangle((int)r.X + 3, (int)r.Y + 2, 30, 16);
            TextRenderer.DrawText(g, title, fontSmall, numRect, Color.FromArgb(90, 100, 120),
                TextFormatFlags.Left | TextFormatFlags.Top);

            // 映射摘要居中；没有映射时只显示编号
            string body = (k.Index >= 0 && k.Index < _texts.Length) ? _texts[k.Index] : null;
            if (!string.IsNullOrEmpty(body))
            {
                var bodyRect = new Rectangle((int)r.X + 2, (int)r.Y + 12, (int)r.Width - 4, (int)r.Height - 14);

                //按宽度自动缩字号：先试正常大小，放不下就换小一号，最后才省略号
                Font use = fontMain;
                if (TextRenderer.MeasureText(body, fontMain).Width > bodyRect.Width - 2) use = fontMain2;
                if (TextRenderer.MeasureText(body, fontMain2).Width > bodyRect.Width - 2) use = fontMain3;

                TextRenderer.DrawText(g, body, use, bodyRect, text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            string marker = k.NeedsRotation ? "↻" : (k.KeyType == 2 ? "◉" : null);
            if (marker != null)
                TextRenderer.DrawText(g, marker, fontSmall,
                    new Point((int)r.Right - 14, (int)r.Y + 1), Color.FromArgb(170, 120, 40));
        }

        /// <summary>
        /// 按可用区域算出「刚好装下布局」的面板尺寸（含边距）。
        /// 1u 的像素受 MaxPixelsPerUnit 限制，所以小配列不会把键撑得过大。
        /// </summary>
        public Size PreferredContentSize(int availWidth, int availHeight)
        {
            if (_layout == null || _layout.Count == 0) return new Size(0, 0);

            float ppu = Math.Min((availWidth - 24f) / _layout.WidthU, (availHeight - 24f) / _layout.HeightU);
            if (MaxPixelsPerUnit > 0 && ppu > MaxPixelsPerUnit) ppu = MaxPixelsPerUnit;
            if (ppu < 6f) ppu = 6f;

            return new Size((int)Math.Ceiling(_layout.WidthU * ppu) + 24,
                            (int)Math.Ceiling(_layout.HeightU * ppu) + 24);
        }

        // ------------------------------------------------------------ 交互
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_layout == null || e.Button != MouseButtons.Left) return;

            float ppu = PixelsPerUnit();
            float ox = Math.Max(8f, (ClientSize.Width - _layout.WidthU * ppu) / 2f);
            float oy = Math.Max(8f, (ClientSize.Height - _layout.HeightU * ppu) / 2f);

            foreach (var k in _layout.Keys)
            {
                var r = new RectangleF(ox + k.UnitsX * ppu, oy + k.UnitsY * ppu, k.UnitsW * ppu, k.UnitsH * ppu);
                if (!r.Contains(e.Location)) continue;
                if (KeySelected != null) KeySelected(k.Index);
                return;
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (_layout == null || e.Button != MouseButtons.Left) return;
            if (KeyDoubleClicked != null) KeyDoubleClicked(_current);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_layout == null) return;
            _zoom *= e.Delta > 0 ? 1.1f : 1f / 1.1f;
            if (_zoom < 0.5f) _zoom = 0.5f;
            if (_zoom > 3f) _zoom = 3f;
            Invalidate();
        }
    }
}
