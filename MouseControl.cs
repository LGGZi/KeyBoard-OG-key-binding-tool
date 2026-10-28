using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KeyboardOG
{
    /// <summary>
    /// 鼠标按键可视化选择控件（320 × 430）。
    ///
    /// 固件里鼠标映射是 report.mouseButtons |= 1 &lt;&lt; offset，
    /// 而 SubHID.cpp 的鼠标描述符（Report ID 3）声明了 3 个按钮：
    ///   bit0 = 左键, bit1 = 右键, bit2 = 中键（滚轮按下）
    /// </summary>
    public class MouseControl : UserControl, IPickerControl
    {
        private const int CanvasWidth = 320;
        private const int CanvasHeight = 430;

        private readonly Rectangle _bodyRect = new Rectangle(45, 32, 230, 316);
        private readonly Dictionary<byte, Rectangle> _hitAreas = new Dictionary<byte, Rectangle>();
        private readonly Dictionary<byte, MapBehavior> _selected = new Dictionary<byte, MapBehavior>();

        private byte? _hover;
        private bool _selectionMode = true;

        public MouseControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(245, 245, 245);
            Size = new Size(CanvasWidth, CanvasHeight);
            BuildHitAreas();
        }

        public MapDevice Device { get { return MapDevice.Mouse; } }

        public event EventHandler<PickerPickedEventArgs> Picked;

        /// <summary>本控件只有选中模式；该属性仅控制鼠标悬停时的反馈。</summary>
        public bool SelectionMode
        {
            get { return _selectionMode; }
            set { _selectionMode = value; Invalidate(); }
        }

        public void SetSelection(IList<Binding> bindings)
        {
            _selected.Clear();
            if (bindings != null)
            {
                foreach (var b in bindings)
                {
                    byte off = BindingCatalog.ToPickerOffset(Device, b.Offset);
                    MapBehavior old;
                    if (!_selected.TryGetValue(off, out old) || old == MapBehavior.None)
                        _selected[off] = b.Behavior;
                }
            }
            Invalidate();
        }

        private MapBehavior BehaviorOf(byte offset)
        {
            MapBehavior b;
            return _selected.TryGetValue(offset, out b) ? b : MapBehavior.None;
        }

        private void BuildHitAreas()
        {
            int top = _bodyRect.Y + 12;
            int halfWidth = _bodyRect.Width / 2;
            int wheelHalf = 18;
            int buttonHeight = 118;

            // 左键 / 右键：上半部分左右两块
            _hitAreas[0] = new Rectangle(_bodyRect.X + 2, top,
                halfWidth - wheelHalf - 8, buttonHeight);
            _hitAreas[1] = new Rectangle(_bodyRect.X + halfWidth + wheelHalf + 8, top,
                _bodyRect.Width - halfWidth - wheelHalf - 10, buttonHeight);
            // 中键：中间细长条
            _hitAreas[2] = new Rectangle(_bodyRect.X + halfWidth - wheelHalf, top + 8,
                wheelHalf * 2, buttonHeight - 26);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            // 鼠标外壳：顶部圆角大、底部圆角小
            using (var body = BuildBodyPath())
            {
                using (var brush = new SolidBrush(PickerPalette.Body))
                    g.FillPath(brush, body);

                // 按键块裁到外壳内部，这样最外圈轮廓仍然由外壳决定。
                g.SetClip(body, CombineMode.Intersect);
                DrawPad(g, 0, L.T("左键"), "bit0");
                DrawPad(g, 1, L.T("右键"), "bit1");
                DrawPad(g, 2, L.T("中键"), "bit2");
                g.ResetClip();

                using (var pen = new Pen(PickerPalette.Edge, 1.6f))
                    g.DrawPath(pen, body);
            }

            PickerDraw.Note(g, L.T("鼠标按键映射：offset 0/1/2 → mouseButtons bit0/1/2"),
                15, CanvasHeight - 56, CanvasWidth - 30);
            PickerDraw.Note(g, L.T("（固件未实现鼠标 X/Y 轴与滚轮量映射）"),
                15, CanvasHeight - 36, CanvasWidth - 30);
        }

        private GraphicsPath BuildBodyPath()
        {
            var body = new GraphicsPath();
            int r = 105, br = 48;
            body.AddArc(_bodyRect.X, _bodyRect.Y, r * 2, r * 2, 180, 90);
            body.AddLine(_bodyRect.X + r, _bodyRect.Y, _bodyRect.Right - r, _bodyRect.Y);
            body.AddArc(_bodyRect.Right - r * 2, _bodyRect.Y, r * 2, r * 2, 270, 90);
            body.AddLine(_bodyRect.Right, _bodyRect.Y + r, _bodyRect.Right, _bodyRect.Bottom - br);
            body.AddArc(_bodyRect.Right - br * 2, _bodyRect.Bottom - br * 2, br * 2, br * 2, 0, 90);
            body.AddLine(_bodyRect.Right - br, _bodyRect.Bottom, _bodyRect.X + br, _bodyRect.Bottom);
            body.AddArc(_bodyRect.X, _bodyRect.Bottom - br * 2, br * 2, br * 2, 90, 90);
            body.AddLine(_bodyRect.X, _bodyRect.Bottom - br, _bodyRect.X, _bodyRect.Y + r);
            body.CloseFigure();
            return body;
        }

        private void DrawPad(Graphics g, byte offset, string text, string sub)
        {
            Rectangle rect;
            if (!_hitAreas.TryGetValue(offset, out rect)) return;
            PickerDraw.Pad(g, rect, text, sub, _selected.ContainsKey(offset), BehaviorOf(offset), true, _hover == offset, false);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            byte? hit = HitTest(e.Location);
            if (hit != _hover)
            {
                _hover = hit;
                Cursor = hit.HasValue ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hover.HasValue)
            {
                _hover = null;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            //左键 = 替换选中项，右键 / Shift+左键 = 追加
            if (!PickerGesture.IsPick(e)) return;
            bool addOnly = PickerGesture.IsAddOnly(e);

            byte? hit = HitTest(e.Location);
            if (!hit.HasValue) return;
            if (Picked != null) Picked(this, new PickerPickedEventArgs(hit.Value, Label(hit.Value), addOnly));
        }

        private byte? HitTest(Point p)
        {
            foreach (var kv in _hitAreas)
                if (kv.Value.Contains(p)) return kv.Key;
            return null;
        }

        private static string Label(byte offset)
        {
            switch (offset)
            {
                case 0: return "左键";
                case 1: return "右键";
                case 2: return "中键";
                default: return "?" + offset;
            }
        }
    }
}
