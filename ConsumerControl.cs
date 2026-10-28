using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KeyboardOG
{
    /// <summary>
    /// 多媒体（消费类）按键可视化选择控件（520 × 300）。
    ///
    /// 固件里是 report.mediaButtons |= 1 &lt;&lt; offset，
    /// 而 SubHID.cpp 的消费类描述符（Report ID 5）按顺序声明了 6 个用法：
    ///   bit0 Mute、bit1 Volume Down、bit2 Volume Up、
    ///   bit3 Play/Pause、bit4 Next Track、bit5 Previous Track
    /// </summary>
    public class ConsumerControl : UserControl, IPickerControl
    {
        private const int CanvasWidth = 520;
        private const int CanvasHeight = 300;

        private sealed class Pad
        {
            public Rectangle Rect;
            public string Text;
            public byte Offset;
        }

        private readonly List<Pad> _pads = new List<Pad>();
        private readonly Dictionary<byte, MapBehavior> _selected = new Dictionary<byte, MapBehavior>();
        private byte? _hover;
        private bool _selectionMode = true;

        public ConsumerControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(245, 245, 245);
            Size = new Size(CanvasWidth, CanvasHeight);
            BuildPads();
        }

        public MapDevice Device { get { return MapDevice.Consumer; } }

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
            int w = 152, h = 74, gapX = 16, gapY = 18;
            int x0 = 20, y0 = 34;

            //这里存中文原文：控件是 InitializeComponent() 里建的，那时候语言还没读出来；
            //真正翻译放在 OnPaint 里（L.T(pad.Text)），这样切换语言能立刻跟上。
            string[] names = { "静音", "音量 -", "音量 +", "播放 / 暂停", "下一曲", "上一曲" };
            for (byte i = 0; i < names.Length; i++)
            {
                int col = i % 3;
                int row = i / 3;
                _pads.Add(new Pad
                {
                    Rect = new Rectangle(x0 + col * (w + gapX), y0 + row * (h + gapY), w, h),
                    Text = names[i],
                    Offset = i,
                });
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            foreach (var pad in _pads)
            {
                PickerDraw.Pad(g, pad.Rect, L.T(pad.Text), "bit" + pad.Offset,
                               _selected.ContainsKey(pad.Offset), BehaviorOf(pad.Offset),
                               true, _hover == pad.Offset, false);
            }

            PickerDraw.Note(g, L.T("多媒体映射：offset 0~5 按描述符顺序 → mediaButtons bit0~bit5"),
                20, CanvasHeight - 40, CanvasWidth - 40);
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
                if (!pad.Rect.Contains(e.Location)) continue;
                if (Picked != null) Picked(this, new PickerPickedEventArgs(pad.Offset, pad.Text, addOnly));
                return;
            }
        }

        private byte? HitTest(Point p)
        {
            foreach (var pad in _pads)
                if (pad.Rect.Contains(p)) return pad.Offset;
            return null;
        }
    }
}
