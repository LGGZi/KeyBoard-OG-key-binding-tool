using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KeyboardOG
{
    /// <summary>
    /// 手柄按键可视化选择控件（580 × 420）。
    ///
    /// 固件里手柄映射是 report.xboxButtons |= 1 &lt;&lt; offset（uint16），
    /// 而 SubHID.cpp 的 Xbox 描述符（Report ID 1）按顺序声明了 16 个按钮，
    /// 所以 offset 0~15 依次对应 B1~B16。
    ///
    /// 名称里给出的 (A)(B)(X)(Y)… 是常见的 Xbox 习惯叫法，
    /// 实际游戏里看到的按钮号以描述符顺序（B1~B16）为准 —— 先按 B 号验证一次，
    /// 如果和习惯叫法不一致，只需要把下面的按钮文字改掉即可，协议本身不受影响。
    ///
    /// 扳机（LT/RT）在协议里是 MAP_TYPE_CONTROLLER_LEFT/RIGHT_TRIGGER(0x0B/0x0C)，
    /// 固件的 keyOperation() 没有实现这两个分支，而且未实现的分支会让固件死循环，
    /// 因此本控件不提供它们。
    /// </summary>
    public class GamepadControl : UserControl, IPickerControl
    {
        private const int CanvasWidth = 580;
        private const int CanvasHeight = 420;

        private sealed class Pad
        {
            public Rectangle Rect;
            public string Text;
            public byte Offset;
            public bool Enabled;
            public bool Circle;
        }

        private readonly List<Pad> _pads = new List<Pad>();
        private readonly Dictionary<byte, MapBehavior> _selected = new Dictionary<byte, MapBehavior>();
        private byte? _hover;
        private bool _selectionMode = true;

        public GamepadControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(245, 245, 245);
            Size = new Size(CanvasWidth, CanvasHeight);
            BuildPads();
        }

        public MapDevice Device { get { return MapDevice.Gamepad; } }

        public event EventHandler<PickerPickedEventArgs> Picked;

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

        private void BuildPads()
        {
            // ---- 未实现：模拟扳机 ----
            _pads.Add(new Pad { Rect = new Rectangle(36, 20, 96, 26), Text = "LT 未实现", Offset = 0x0B, Enabled = false });
            _pads.Add(new Pad { Rect = new Rectangle(448, 20, 96, 26), Text = "RT 未实现", Offset = 0x0C, Enabled = false });

            // ---- 肩键 ----
            _pads.Add(new Pad { Rect = new Rectangle(36, 56, 118, 36), Text = "LB", Offset = 4, Enabled = true });
            _pads.Add(new Pad { Rect = new Rectangle(426, 56, 118, 36), Text = "RB", Offset = 5, Enabled = true });

            // ---- 摇杆按下（LS / RS）----
            _pads.Add(new Pad { Rect = new Rectangle(136, 92, 68, 68), Text = "LS", Offset = 8, Enabled = true, Circle = true });
            _pads.Add(new Pad { Rect = new Rectangle(376, 92, 68, 68), Text = "RS", Offset = 9, Enabled = true, Circle = true });

            // ---- 方向键 ----
            _pads.Add(new Pad { Rect = new Rectangle(114, 214, 56, 56), Text = "↑", Offset = 10, Enabled = true });
            _pads.Add(new Pad { Rect = new Rectangle(56, 272, 56, 56), Text = "←", Offset = 12, Enabled = true });
            _pads.Add(new Pad { Rect = new Rectangle(172, 272, 56, 56), Text = "→", Offset = 13, Enabled = true });
            _pads.Add(new Pad { Rect = new Rectangle(114, 330, 56, 56), Text = "↓", Offset = 11, Enabled = true });

            // ---- ABXY ----
            _pads.Add(new Pad { Rect = new Rectangle(416, 214, 56, 56), Text = "Y", Offset = 3, Enabled = true });
            _pads.Add(new Pad { Rect = new Rectangle(358, 272, 56, 56), Text = "X", Offset = 2, Enabled = true });
            _pads.Add(new Pad { Rect = new Rectangle(474, 272, 56, 56), Text = "B", Offset = 1, Enabled = true });
            _pads.Add(new Pad { Rect = new Rectangle(416, 330, 56, 56), Text = "A", Offset = 0, Enabled = true });

            // ---- 中间功能键 ----
            _pads.Add(new Pad { Rect = new Rectangle(266, 208, 56, 32), Text = "Guide", Offset = 14, Enabled = true });
            _pads.Add(new Pad { Rect = new Rectangle(240, 258, 52, 32), Text = "Back", Offset = 6, Enabled = true });
            _pads.Add(new Pad { Rect = new Rectangle(296, 258, 52, 32), Text = "Start", Offset = 7, Enabled = true });
            _pads.Add(new Pad { Rect = new Rectangle(262, 320, 64, 32), Text = "B16", Offset = 15, Enabled = true });
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            using (var body = PickerDraw.RoundedRect(new Rectangle(16, 10, CanvasWidth - 32, CanvasHeight - 46), 46))
            {
                PickerDraw.Body(g, body);
            }

            foreach (var pad in _pads)
            {
                bool selectable = pad.Enabled;
                string sub = selectable ? "bit" + pad.Offset : null;
                MapBehavior behavior = selectable ? BehaviorOf(pad.Offset) : MapBehavior.None;
                PickerDraw.Pad(g, pad.Rect, L.T(pad.Text), sub, behavior, selectable,
                               selectable && _hover == pad.Offset, pad.Circle);
            }

            PickerDraw.Note(g, L.T("手柄映射：offset 0~15 → xboxButtons bit0~bit15（描述符里的 B1~B16）"),
                16, CanvasHeight - 30, CanvasWidth - 32);
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

            foreach (var pad in _pads)
            {
                if (!pad.Enabled || !pad.Rect.Contains(e.Location)) continue;
                if (Picked != null) Picked(this, new PickerPickedEventArgs(pad.Offset, pad.Text, addOnly));
                return;
            }
        }

        private byte? HitTest(Point p)
        {
            foreach (var pad in _pads)
                if (pad.Enabled && pad.Rect.Contains(p)) return pad.Offset;
            return null;
        }
    }
}
