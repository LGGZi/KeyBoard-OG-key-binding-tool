using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KeyboardOG
{
    /// <summary>可视化选键控件被点击时抛出的参数。</summary>
    public sealed class PickerPickedEventArgs : EventArgs
    {
        /// <summary>
        /// 该设备类型下“选键控件用的”偏移量。
        /// 对键盘来说就是 HID Usage ID（修饰键是 224~231）；
        /// 真正下发给固件前要用 BindingCatalog.ToWireOffset() 换算。
        /// </summary>
        public byte Offset;

        /// <summary>可读名称，例如 "A"。</summary>
        public string Label;

        /// <summary>
        /// true = 只做"追加"（右键 / Shift+左键）。
        /// false = 左键：替换左侧列表里选中的那一条（没选中就追加）。
        /// </summary>
        public bool AddOnly;

        public PickerPickedEventArgs(byte offset, string label)
            : this(offset, label, false)
        {
        }

        public PickerPickedEventArgs(byte offset, string label, bool addOnly)
        {
            Offset = offset;
            Label = label;
            AddOnly = addOnly;
        }
    }

    /// <summary>
    /// 选键控件上的鼠标手势约定（四个控件共用，免得各写一遍）：
    ///   · 左键            —— 替换左侧映射列表里选中的那一条（没选中时就是追加）
    ///   · 右键 / Shift+左键 —— 追加一条
    /// </summary>
    public static class PickerGesture
    {
        /// <summary>这个按键事件算不算"点了一下选键控件"。</summary>
        public static bool IsPick(MouseEventArgs e)
        {
            return e.Button == MouseButtons.Left || e.Button == MouseButtons.Right;
        }

        /// <summary>是不是"只追加"（右键 / Shift+左键）。</summary>
        public static bool IsAddOnly(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) return true;
            if (e.Button == MouseButtons.Left && (Control.ModifierKeys & Keys.Shift) == Keys.Shift) return true;
            return false;
        }
    }

    /// <summary>
    /// 所有“可视化选键”控件的统一接口，主窗体不关心具体是键盘 / 鼠标 / 手柄 / 多媒体。
    /// </summary>
    public interface IPickerControl
    {
        MapDevice Device { get; }

        event EventHandler<PickerPickedEventArgs> Picked;

        /// <summary>
        /// true 时点击表示“选中一个映射”。
        /// 键盘控件还有 false 模式（模拟按下/抬起，保留原有行为）；
        /// 鼠标 / 手柄 / 多媒体控件只有选中模式，该属性仅用于控制悬停反馈。
        /// </summary>
        bool SelectionMode { get; set; }

        /// <summary>高亮已经选中的偏移量集合（用 Picked 那套偏移量，即键盘用 Usage ID）。</summary>
        void SetSelection(IList<byte> offsets);
    }

    /// <summary>选键控件统一配色。</summary>
    internal static class PickerPalette
    {
        public static readonly Color Normal       = Color.White;
        public static readonly Color Modifier     = Color.FromArgb(225, 225, 225);
        public static readonly Color Hover        = Color.FromArgb(240, 248, 255);
        public static readonly Color Pressed      = Color.FromArgb(150, 200, 255);
        public static readonly Color Selected     = Color.FromArgb(120, 195, 130);
        public static readonly Color SelectedEdge = Color.FromArgb(46, 125, 60);
        public static readonly Color Disabled     = Color.FromArgb(234, 234, 234);
        public static readonly Color DisabledText = Color.FromArgb(170, 170, 170);
        public static readonly Color Text         = Color.FromArgb(30, 30, 30);
        public static readonly Color Body         = Color.FromArgb(248, 248, 250);
        public static readonly Color Edge         = Color.FromArgb(150, 150, 155);
        public static readonly Color SubText      = Color.FromArgb(110, 110, 115);
    }

    /// <summary>鼠标 / 手柄 / 多媒体控件共用的绘制与命中测试辅助。</summary>
    internal static class PickerDraw
    {
        public static readonly Font PadFontSmall = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular);
        public static readonly Font PadFont      = new Font("Microsoft YaHei UI", 10f, FontStyle.Regular);
        public static readonly Font PadFontBig   = new Font("Microsoft YaHei UI", 11.5f, FontStyle.Regular);
        public static readonly Font SubFont      = new Font("Segoe UI", 7f, FontStyle.Regular);
        public static readonly Font NoteFont     = new Font("Microsoft YaHei UI", 8f, FontStyle.Regular);

        public static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0) { path.AddRectangle(r); return path; }

            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;

            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// 画一个可选中的“按键块”。
        ///
        /// 文字分两层：主标题画在上部，副标题（bit 下标）画在底部固定高度里。
        /// 之前主标题的矩形占满整个高度、副标题又画在底部，两者必然叠字 —— 这次修掉。
        /// </summary>
        public static void Pad(Graphics g, Rectangle rect, string text, string sub,
                               bool selected, bool enabled, bool hover, bool circle)
        {
            Color fill, edge, fore;
            if (!enabled)
            {
                fill = PickerPalette.Disabled;
                edge = Color.FromArgb(205, 205, 205);
                fore = PickerPalette.DisabledText;
            }
            else if (selected)
            {
                fill = PickerPalette.Selected;
                edge = PickerPalette.SelectedEdge;
                fore = Color.FromArgb(20, 60, 25);
            }
            else if (hover)
            {
                fill = PickerPalette.Hover;
                edge = PickerPalette.Edge;
                fore = PickerPalette.Text;
            }
            else
            {
                fill = PickerPalette.Normal;
                edge = PickerPalette.Edge;
                fore = PickerPalette.Text;
            }

            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = circle
                ? EllipsePath(rect)
                : RoundedRect(rect, Math.Min(10, Math.Min(rect.Width, rect.Height) / 3)))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(edge, selected ? 2f : 1f))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }

            // ---- 文字区域：主标题在上、副标题在下，互不重叠 ----
            bool hasSub = !string.IsNullOrEmpty(sub);
            int subH = hasSub ? 15 : 0;
            var textRect = new Rectangle(rect.X + 2, rect.Y + 2,
                                         Math.Max(1, rect.Width - 4),
                                         Math.Max(1, rect.Height - subH - 4));

            Font labelFont = PadFontBig;
            if (rect.Height < 60 || rect.Width < 90) labelFont = PadFont;
            if (rect.Height < 44 || rect.Width < 62) labelFont = PadFontSmall;
            if (text != null && text.Length > 4) labelFont = PadFontSmall;

            using (var brush = new SolidBrush(fore))
            using (var fmt = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap,
            })
            {
                g.DrawString(text ?? string.Empty, labelFont, brush, textRect, fmt);
            }

            if (hasSub)
            {
                using (var brush = new SolidBrush(PickerPalette.SubText))
                {
                    var subRect = new Rectangle(rect.X + 2, rect.Bottom - subH - 1,
                                                Math.Max(1, rect.Width - 4), subH);
                    using (var fmt = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center,
                        FormatFlags = StringFormatFlags.NoWrap,
                    })
                    {
                        g.DrawString(sub, SubFont, brush, subRect, fmt);
                    }
                }
            }
        }

        private static GraphicsPath EllipsePath(Rectangle r)
        {
            var p = new GraphicsPath();
            p.AddEllipse(r);
            return p;
        }

        /// <summary>画设备外形轮廓（鼠标外壳 / 手柄外壳）。</summary>
        public static void Body(Graphics g, GraphicsPath path)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(PickerPalette.Body))
            using (var pen = new Pen(PickerPalette.Edge, 1.6f))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }
        }

        public static void Note(Graphics g, string text, int x, int y, int width)
        {
            using (var brush = new SolidBrush(PickerPalette.SubText))
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center })
            {
                g.DrawString(text, NoteFont, brush, new RectangleF(x, y, width, 30), fmt);
            }
        }
    }
}
