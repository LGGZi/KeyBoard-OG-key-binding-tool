using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KLEconvert
{
    /// <summary>按真实配列绘制按键的只读画布：点击选中，滚轮缩放。</summary>
    public class LayoutCanvas : Control
    {
        private KleLayout _layout;
        private KeyItem _selected;
        private float _zoom = 1.0f;

        public event EventHandler SelectionChanged;

        public LayoutCanvas()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.FromArgb(250, 250, 252);
            AllowDrop = true;
        }

        public new KleLayout Layout
        {
            get { return _layout; }
            set
            {
                _layout = value;
                _selected = null;
                _zoom = 1.0f;
                Invalidate();
                RaiseSelection();
            }
        }

        public KeyItem Selected
        {
            get { return _selected; }
            set
            {
                if (ReferenceEquals(_selected, value)) return;
                _selected = value;
                Invalidate();
                RaiseSelection();
            }
        }

        private void RaiseSelection()
        {
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }

        private float PixelsPerUnit(Rectangle area)
        {
            if (_layout == null || _layout.WidthU <= 0 || _layout.HeightU <= 0) return 40f;
            float sx = (area.Width - 24f) / (float)_layout.WidthU;
            float sy = (area.Height - 24f) / (float)_layout.HeightU;
            float ppu = Math.Min(sx, sy);
            if (ppu < 6f) ppu = 6f;
            if (ppu > 96f) ppu = 96f;
            return ppu * _zoom;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            if (_layout == null || _layout.Keys.Count == 0)
            {
                TextRenderer.DrawText(g, "把 keyboard-layout-editor 导出的 .json 拖到这里",
                    new Font(Font.FontFamily, 12f), ClientRectangle,
                    Color.FromArgb(140, 140, 150),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            float ppu = PixelsPerUnit(ClientRectangle);
            float w = (float)_layout.WidthU * ppu;
            float h = (float)_layout.HeightU * ppu;
            float ox = (ClientSize.Width - w) / 2f;
            float oy = (ClientSize.Height - h) / 2f;
            if (ox < 8f) ox = 8f;
            if (oy < 8f) oy = 8f;

            using (var fontIndex = new Font("Segoe UI", Math.Max(7f, Math.Min(13f, ppu * 0.22f)), FontStyle.Bold))
            using (var fontName = new Font("Segoe UI", Math.Max(6f, Math.Min(11f, ppu * 0.18f))))
            {
                foreach (var k in _layout.Keys)
                {
                    var rect = new RectangleF(ox + k.Ux / 4f * ppu, oy + k.Uy / 4f * ppu,
                                              k.Uw / 4f * ppu - 2f, k.Uh / 4f * ppu - 2f);
                    bool sel = ReferenceEquals(k, _selected);
                    DrawKey(g, rect, k, sel, fontIndex, fontName);
                }
            }
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

        private void DrawKey(Graphics g, RectangleF r, KeyItem k, bool selected, Font fontIndex, Font fontName)
        {
            Color fill = selected ? Color.FromArgb(198, 222, 255) : Color.White;
            Color border = selected ? Color.FromArgb(40, 100, 200) : Color.FromArgb(170, 175, 185);
            if (k.Index < 0 && !selected) fill = Color.FromArgb(246, 246, 248);

            using (var path = RoundRect(r, Math.Min(6f, r.Height * 0.15f)))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border, selected ? 2f : 1f))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }

            string title = k.Index >= 0 ? "#" + k.Index : "—";
            string sub = k.Name;
            var titleRect = new RectangleF(r.X + 2, r.Y + 2, r.Width - 4, r.Height * (sub.Length > 0 ? 0.6f : 1f));
            var subRect = new RectangleF(r.X + 2, r.Y + r.Height * 0.58f, r.Width - 4, r.Height * 0.4f);

            TextRenderer.DrawText(g, title, fontIndex, Rectangle.Round(titleRect),
                k.Index >= 0 ? Color.FromArgb(20, 60, 130) : Color.FromArgb(160, 160, 168),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (sub.Length > 0)
                TextRenderer.DrawText(g, sub, fontName, Rectangle.Round(subRect),
                    Color.FromArgb(90, 95, 105),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (k.Rotated)
                TextRenderer.DrawText(g, "↻", fontName, new Point((int)r.Right - 16, (int)r.Y + 2), Color.FromArgb(180, 120, 40));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_layout == null || e.Button != MouseButtons.Left) return;

            float ppu = PixelsPerUnit(ClientRectangle);
            float w = (float)_layout.WidthU * ppu;
            float h = (float)_layout.HeightU * ppu;
            float ox = Math.Max(8f, (ClientSize.Width - w) / 2f);
            float oy = Math.Max(8f, (ClientSize.Height - h) / 2f);

            KeyItem hit = null;
            foreach (var k in _layout.Keys)
            {
                var r = new RectangleF(ox + k.Ux / 4f * ppu, oy + k.Uy / 4f * ppu,
                                       k.Uw / 4f * ppu, k.Uh / 4f * ppu);
                if (r.Contains(e.Location)) { hit = k; break; }
            }
            Selected = hit;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_layout == null) return;
            _zoom *= e.Delta > 0 ? 1.1f : 1f / 1.1f;
            if (_zoom < 0.4f) _zoom = 0.4f;
            if (_zoom > 4f) _zoom = 4f;
            Invalidate();
        }
    }
}
