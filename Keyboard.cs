using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace KeyboardOG
{
    public partial class Keyboard : UserControl, IPickerControl
    {
        // ============================================================
        // 1. 主键区布局（Row 0~5，共 15 列）
        // ============================================================
        private static readonly string[] MainLayout =
        {
            "Esc|F1|F2|F3|F4|F5|F6|F7|F8|F9|F10|F11|F12|_|_",
            "`|1|2|3|4|5|6|7|8|9|0|-|=|Backspace:2",
            "Tab:1.5|Q|W|E|R|T|Y|U|I|O|P|[|]|\\:1.5",
            "Caps:1.75|A|S|D|F|G|H|J|K|L|;|'|Enter:2.25",
            "Shift_L:2.25|Z|X|C|V|B|N|M|,|.|/|Shift_R:2.75",
            "Ctrl_L:1.25|Win_L:1.25|Alt_L:1.25|Space:6.25|Alt_R:1.25|Win_R:1.25|Menu:1.25|Ctrl_R:1.25"
        };

        // ============================================================
        // 2. 功能键区布局（6 行 × 3 列，null = 空位）
        // ============================================================
        private static readonly string[,] FuncLayout = new string[6, 3]
        {
            { "PrintScreen", "ScrollLock", "Pause"    },
            { "Insert",      "Home",       "PageUp"   },
            { "Delete",      "End",        "PageDown" },
            { null,          null,         null       },
            { null,          "Up",         null       },
            { "Left",        "Down",       "Right"    }
        };

        // ============================================================
        // 3. 小键盘布局（5 行 × 4 列，null = 被跨行键占用）
        // ============================================================
        private static readonly string[,] NumpadLayout = new string[5, 4]
        {
            { "NumLock", "KpSlash", "KpAsterisk", "KpMinus" },
            { "Kp7",     "Kp8",     "Kp9",        "KpPlus"  },
            { "Kp4",     "Kp5",     "Kp6",        null      },
            { "Kp1",     "Kp2",     "Kp3",        "KpEnter" },
            { "Kp0",     null,      "KpDot",      null      }
        };

        // ============================================================
        // 4. HID Usage ID 映射表（十进制）
        // 数据源：USB HID Usage Tables, Keyboard/Keypad Page (0x07)
        // ============================================================
        private static readonly Dictionary<string, int> HidIds = new Dictionary<string, int>
        {
            // 字母 A-Z: 4-29
            { "A", 4 }, { "B", 5 }, { "C", 6 }, { "D", 7 }, { "E", 8 },
            { "F", 9 }, { "G", 10 }, { "H", 11 }, { "I", 12 }, { "J", 13 },
            { "K", 14 }, { "L", 15 }, { "M", 16 }, { "N", 17 }, { "O", 18 },
            { "P", 19 }, { "Q", 20 }, { "R", 21 }, { "S", 22 }, { "T", 23 },
            { "U", 24 }, { "V", 25 }, { "W", 26 }, { "X", 27 }, { "Y", 28 }, { "Z", 29 },

            // 数字 1-0: 30-39
            { "1", 30 }, { "2", 31 }, { "3", 32 }, { "4", 33 }, { "5", 34 },
            { "6", 35 }, { "7", 36 }, { "8", 37 }, { "9", 38 }, { "0", 39 },

            // 主键区符号与特殊键
            { "Enter", 40 }, { "Esc", 41 }, { "Backspace", 42 }, { "Tab", 43 },
            { "Space", 44 }, { "-", 45 }, { "=", 46 }, { "[", 47 }, { "]", 48 },
            { "\\", 49 }, { ";", 51 }, { "'", 52 }, { "`", 53 },
            { ",", 54 }, { ".", 55 }, { "/", 56 }, { "Caps", 57 },

            // F1-F12: 58-69
            { "F1", 58 }, { "F2", 59 }, { "F3", 60 }, { "F4", 61 },
            { "F5", 62 }, { "F6", 63 }, { "F7", 64 }, { "F8", 65 },
            { "F9", 66 }, { "F10", 67 }, { "F11", 68 }, { "F12", 69 },

            // 功能键区: 70-82
            { "PrintScreen", 70 }, { "ScrollLock", 71 }, { "Pause", 72 },
            { "Insert", 73 }, { "Home", 74 }, { "PageUp", 75 },
            { "Delete", 76 }, { "End", 77 }, { "PageDown", 78 },
            { "Right", 79 }, { "Left", 80 }, { "Down", 81 }, { "Up", 82 },

            // 小键盘: 83-99
            { "NumLock", 83 }, { "KpSlash", 84 }, { "KpAsterisk", 85 },
            { "KpMinus", 86 }, { "KpPlus", 87 }, { "KpEnter", 88 },
            { "Kp1", 89 }, { "Kp2", 90 }, { "Kp3", 91 },
            { "Kp4", 92 }, { "Kp5", 93 }, { "Kp6", 94 },
            { "Kp7", 95 }, { "Kp8", 96 }, { "Kp9", 97 },
            { "Kp0", 98 }, { "KpDot", 99 },

            // 修饰键: 224-231
            { "Ctrl_L", 224 }, { "Shift_L", 225 }, { "Alt_L", 226 }, { "Win_L", 227 },
            { "Ctrl_R", 228 }, { "Shift_R", 229 }, { "Alt_R", 230 }, { "Win_R", 231 },
            { "Menu", 101 },
        };

        // ============================================================
        // 5. 显示文字映射（长文字用缩写，防换行）
        // ============================================================
        private static readonly Dictionary<string, string> DisplayTextMap = new Dictionary<string, string>
        {
            { "PrintScreen", "PrtSc" },
            { "ScrollLock",  "ScrLk" },
            { "PageUp",      "PgUp"  },
            { "PageDown",    "PgDn"  },
            { "Insert",      "Ins"   },
            { "Delete",      "Del"   },
            { "NumLock",     "NumLk" },
            { "Backspace",   "Bksp"  },
            { "KpSlash",     "/"     },
            { "KpAsterisk",  "*"     },
            { "KpMinus",     "-"     },
            { "KpPlus",      "+"     },
            { "KpEnter",     "Ent"   },
            { "KpDot",       "."     },
            { "Kp0", "0" }, { "Kp1", "1" }, { "Kp2", "2" }, { "Kp3", "3" },
            { "Kp4", "4" }, { "Kp5", "5" }, { "Kp6", "6" }, { "Kp7", "7" },
            { "Kp8", "8" }, { "Kp9", "9" },
            { "Shift_L", "Shift" }, { "Shift_R", "Shift" },
            { "Ctrl_L",  "Ctrl"  }, { "Ctrl_R",  "Ctrl"  },
            { "Alt_L",   "Alt"   }, { "Alt_R",   "Alt"   },
            { "Win_L",   "Win"   }, { "Win_R",   "Win"   },
            { "Space",   "Space" },
        };

        // ============================================================
        // 6. 尺寸参数
        // ============================================================
        private const int UnitWidth = 44;
        private const int UnitHeight = 44;
        private const int Gap = 4;
        private const int MainColumns = 15;
        private const int FuncColumns = 3;
        private const int NumpadColumns = 4;
        private const int RegionGap = 12;   // 三个大区域之间的间距

        // ============================================================
        // 7. 修饰键与配色
        // ============================================================
        private static readonly HashSet<string> ModifierKeySet = new HashSet<string>
        {
            "Tab", "Caps", "Shift_L", "Shift_R", "Ctrl_L", "Ctrl_R",
            "Alt_L", "Alt_R", "Win_L", "Win_R", "Menu", "Backspace", "Enter",
            "NumLock", "KpEnter"
        };

        private static readonly Color NormalColor = PickerPalette.Normal;
        private static readonly Color ModifierColor = PickerPalette.Modifier;
        private static readonly Color HoverColor = PickerPalette.Hover;
        private static readonly Color PressedColor = PickerPalette.Pressed;

        // ============================================================
        // 8. 状态存储
        // ============================================================
        private readonly Dictionary<string, Button> _keyButtons = new Dictionary<string, Button>();
        private readonly Dictionary<string, bool> _keyStates = new Dictionary<string, bool>();
        private readonly HashSet<int> _selectedHids = new HashSet<int>();
        private readonly List<Font> _ownedFonts = new List<Font>();
        private readonly Dictionary<Button, FontStyle> _buttonFontStyle = new Dictionary<Button, FontStyle>();
        private bool _selectionMode = true;

        // ============================================================
        // 9. 对外事件
        // ============================================================
        public event Action<string, bool, int> KeyStateChanged;
        public event Action<string, int> KeyPressed;
        public event Action<string, int> KeyReleased;
        public event EventHandler<PickerPickedEventArgs> Picked;

        // ============================================================
        // 10. 构造
        // ============================================================
        public Keyboard()
        {
            InitializeComponent();

            AutoScaleMode = AutoScaleMode.None;   // 像素级精确定位，避免 DPI 缩放错位
            DoubleBuffered = true;
            BackColor = Color.FromArgb(245, 245, 245);

            BuildKeyboard();
            RefreshKeyColors();
        }

        // ============================================================
        // 11. 总入口：三个区域分别构建
        // ============================================================
        private void BuildKeyboard()
        {
            SuspendLayout();
            Controls.Clear();
            _keyButtons.Clear();
            _buttonFontStyle.Clear();
            foreach (var f in _ownedFonts) f.Dispose();
            _ownedFonts.Clear();

            int mainWidth = MainColumns * UnitWidth + (MainColumns - 1) * Gap;
            int funcWidth = FuncColumns * UnitWidth + (FuncColumns - 1) * Gap;
            int numpadWidth = NumpadColumns * UnitWidth + (NumpadColumns - 1) * Gap;

            int funcX = mainWidth + RegionGap;
            int numpadX = funcX + funcWidth + RegionGap;

            BuildMainRegion(0, 0);
            BuildFuncRegion(funcX, 0);
            BuildNumpadRegion(numpadX, UnitHeight + Gap);   // 小键盘从 Row 1 开始

            int totalWidth = numpadX + numpadWidth + 2;
            int mainHeight = MainLayout.Length * UnitHeight + (MainLayout.Length - 1) * Gap;
            Size = new Size(totalWidth, mainHeight + 2);

            ResumeLayout();
        }

        // -------- 主键区 --------
        private void BuildMainRegion(int offsetX, int offsetY)
        {
            int y = offsetY;
            foreach (var rowDef in MainLayout)
            {
                int x = offsetX;
                foreach (var keyDef in rowDef.Split('|'))
                {
                    // 空位占位符 "_" 或 ""
                    if (string.IsNullOrEmpty(keyDef) || keyDef == "_")
                    {
                        x += UnitWidth + Gap;
                        continue;
                    }

                    var parts = keyDef.Split(':');
                    string name = parts[0];
                    float units = 1f;
                    if (parts.Length > 1)
                        units = float.Parse(parts[1], CultureInfo.InvariantCulture);

                    int w = (int)(units * UnitWidth + (units - 1) * Gap);
                    Button btn = CreateKeyButton(name, x, y, w, UnitHeight);
                    Controls.Add(btn);
                    _keyButtons[name] = btn;

                    x += w + Gap;
                }
                y += UnitHeight + Gap;
            }
        }

        // -------- 功能键区 --------
        private void BuildFuncRegion(int offsetX, int offsetY)
        {
            for (int r = 0; r < FuncLayout.GetLength(0); r++)
            {
                for (int c = 0; c < FuncLayout.GetLength(1); c++)
                {
                    string name = FuncLayout[r, c];
                    if (name == null) continue;

                    int x = offsetX + c * (UnitWidth + Gap);
                    int y = offsetY + r * (UnitHeight + Gap);

                    Button btn = CreateKeyButton(name, x, y, UnitWidth, UnitHeight);
                    Controls.Add(btn);
                    _keyButtons[name] = btn;
                }
            }
        }

        // -------- 小键盘 --------
        private void BuildNumpadRegion(int offsetX, int offsetY)
        {
            for (int r = 0; r < NumpadLayout.GetLength(0); r++)
            {
                for (int c = 0; c < NumpadLayout.GetLength(1); c++)
                {
                    string name = NumpadLayout[r, c];
                    if (name == null) continue;

                    int x = offsetX + c * (UnitWidth + Gap);
                    int y = offsetY + r * (UnitHeight + Gap);
                    int w = UnitWidth;
                    int h = UnitHeight;

                    if (name == "KpPlus") h = UnitHeight * 2 + Gap;         // + 跨两行
                    else if (name == "KpEnter") h = UnitHeight * 2 + Gap;   // Enter 跨两行
                    else if (name == "Kp0") w = UnitWidth * 2 + Gap;        // 0 跨两列

                    Button btn = CreateKeyButton(name, x, y, w, h);
                    Controls.Add(btn);
                    _keyButtons[name] = btn;
                }
            }
        }

        // ============================================================
        // 12. 创建单个按键
        // ============================================================
        private Button CreateKeyButton(string name, int x, int y, int width, int height)
        {
            string text = GetDisplayText(name);

            var btn = new Button();
            btn.Text = text;
            btn.Tag = name;
            btn.Location = new Point(x, y);
            btn.Size = new Size(width, height);
            btn.FlatStyle = FlatStyle.Flat;
            btn.BackColor = ModifierKeySet.Contains(name) ? ModifierColor : NormalColor;
            btn.TabStop = false;
            btn.Cursor = Cursors.Hand;
            btn.AutoEllipsis = false;
            btn.UseCompatibleTextRendering = false;   // 用 GDI 渲染，文字更清晰
            btn.TextAlign = ContentAlignment.MiddleCenter;
            btn.FlatAppearance.BorderColor = Color.Gray;

            // ---- 关键：按文字长度 + 按钮尺寸自适应字号，避免换行 ----
            ApplyFont(btn, CalcFontSize(text, width), FontStyle.Regular);

            btn.MouseDown += Key_MouseDown;
            btn.MouseUp += Key_MouseUp;
            btn.MouseEnter += Key_MouseEnter;
            btn.MouseLeave += Key_MouseLeave;
            return btn;
        }

        /// <summary>
        /// 设置按钮字体。字体对象由本控件持有并缓存，只有样式真的变化时才新建，
        /// 避免在 MouseEnter/MouseLeave 这种高频路径上反复创建 Font 造成 GDI 句柄泄漏。
        /// </summary>
        private void ApplyFont(Button btn, float size, FontStyle style)
        {
            FontStyle current;
            if (_buttonFontStyle.TryGetValue(btn, out current) && current == style) return;

            var font = new Font("Segoe UI", size, style);
            _ownedFonts.Add(font);
            btn.Font = font;
            _buttonFontStyle[btn] = style;
        }

        /// <summary>根据文字长度与按钮宽度估算安全字号，避免换行</summary>
        private static float CalcFontSize(string text, int buttonWidth)
        {
            int len = text != null ? text.Length : 0;
            if (len == 0) return 9f;

            // 以 44px 宽按钮为基准，宽度越大允许的字号越大
            float baseSize;
            if (len <= 1) baseSize = 11f;
            else if (len == 2) baseSize = 10f;
            else if (len == 3) baseSize = 8.5f;
            else if (len == 4) baseSize = 7.5f;
            else if (len == 5) baseSize = 6.5f;
            else baseSize = 5.5f;

            // 宽按钮（如 Backspace、Enter、Space）可以放大一点
            if (buttonWidth >= UnitWidth * 2) baseSize += 0.5f;
            if (buttonWidth >= UnitWidth * 3) baseSize += 0.5f;

            return baseSize;
        }

        // ============================================================
        // 13. 显示文本
        // ============================================================
        private static string GetDisplayText(string name)
        {
            string text;
            if (DisplayTextMap.TryGetValue(name, out text)) return text;
            return name;
        }

        private static Color BaseColor(string key)
        {
            return ModifierKeySet.Contains(key) ? ModifierColor : NormalColor;
        }

        // ============================================================
        // 14. HID ID 查询
        // ============================================================
        public int GetHidId(string key)
        {
            int id;
            return HidIds.TryGetValue(key, out id) ? id : 0;
        }

        /// <summary>HID Usage ID -> 按键名（供映射列表显示用）。</summary>
        public static readonly IReadOnlyDictionary<int, string> KeyNameByHidId = BuildReverseTable();

        private static Dictionary<int, string> BuildReverseTable()
        {
            var map = new Dictionary<int, string>();
            foreach (var kv in HidIds)
                if (!map.ContainsKey(kv.Value)) map[kv.Value] = kv.Key;
            return map;
        }

        /// <summary>
        /// 该 HID Usage 能否被本板固件正确编码。
        ///
        /// SubHID.cpp 发出的键盘报文是 14 字节：
        ///   keyboardKeys[0..12] = 13 字节普通键（HID usage 0..103，一位一个键）
        ///   keyboardKeys[13]    = 1 字节修饰键（usage 0xE0~0xE7 → bit0~bit7）
        /// 固件 keyOperation() 走 keyboardKeys[offset/8] |= 1 &lt;&lt; (offset%8)，
        /// 所以：
        ///   · 普通键 usage 4..103  → 直接下发 usage           → [0..12]
        ///   · 修饰键 usage 224..231 → 下发 104..111（见 BindingCatalog.ToWireOffset）→ [13]
        /// 两者都支持，页面上不再有不可点的按键。
        /// </summary>
        public static bool IsHidIdSupported(int hidId)
        {
            if (hidId >= 4 && hidId <= 103) return true;     // 普通键区 keyboardKeys[0..12]
            if (hidId >= 224 && hidId <= 231) return true;   // 修饰键区 keyboardKeys[13]
            return false;
        }

        // ============================================================
        // 15. 事件处理
        // ============================================================
        private void Key_MouseDown(object sender, MouseEventArgs e)
        {
            var btn = (Button)sender;
            string key = (string)btn.Tag;

            if (_selectionMode)
            {
                //左键 = 替换选中项，右键 / Shift+左键 = 追加
            if (!PickerGesture.IsPick(e)) return;
            bool addOnly = PickerGesture.IsAddOnly(e);
                int hid = GetHidId(key);
                if (!IsHidIdSupported(hid)) return;      // 固件不支持的按键直接忽略
                if (Picked != null) Picked(this, new PickerPickedEventArgs((byte)hid, key, addOnly));
                return;
            }

            btn.BackColor = PressedColor;
            SetKeyState(key, true);
        }

        private void Key_MouseUp(object sender, MouseEventArgs e)
        {
            var btn = (Button)sender;
            string key = (string)btn.Tag;

            if (_selectionMode)
            {
                RefreshKeyColors();
                return;
            }

            btn.BackColor = btn.ClientRectangle.Contains(btn.PointToClient(Cursor.Position))
                ? HoverColor : BaseColor(key);
            SetKeyState(key, false);
        }

        private void Key_MouseEnter(object sender, EventArgs e)
        {
            if (_selectionMode) return;

            var btn = (Button)sender;
            string key = (string)btn.Tag;
            if (!IsKeyDown(key)) btn.BackColor = HoverColor;
        }

        private void Key_MouseLeave(object sender, EventArgs e)
        {
            if (_selectionMode) { RefreshKeyColors(); return; }

            var btn = (Button)sender;
            string key = (string)btn.Tag;
            if (IsKeyDown(key)) SetKeyState(key, false);
            btn.BackColor = BaseColor(key);
        }

        private void SetKeyState(string key, bool isDown)
        {
            bool old;
            if (_keyStates.TryGetValue(key, out old) && old == isDown) return;
            _keyStates[key] = isDown;

            int hidId = GetHidId(key);

            if (KeyStateChanged != null) KeyStateChanged(key, isDown, hidId);
            if (isDown)
            {
                if (KeyPressed != null) KeyPressed(key, hidId);
            }
            else
            {
                if (KeyReleased != null) KeyReleased(key, hidId);
            }
        }

        // ============================================================
        // 16. 对外 API
        // ============================================================
        public bool IsKeyDown(string key)
        {
            bool v;
            return _keyStates.TryGetValue(key, out v) && v;
        }

        public IReadOnlyDictionary<string, bool> KeyStates
        {
            get { return _keyStates; }
        }

        public void SetKeyHighlight(string key, bool highlighted)
        {
            Button btn;
            if (_keyButtons.TryGetValue(key, out btn))
                btn.BackColor = highlighted ? PressedColor : BaseColor(key);
        }

        public void ClearAllKeys()
        {
            var keys = _keyStates.Keys.ToList();
            foreach (var key in keys) SetKeyState(key, false);

            foreach (var kv in _keyButtons)
                kv.Value.BackColor = BaseColor(kv.Key);
        }

        // ============================================================
        // 17. IPickerControl —— 可视化选键模式
        // ============================================================
        public MapDevice Device { get { return MapDevice.Keyboard; } }

        /// <summary>true（默认）= 点击即选中一个映射；false = 原来的按下/抬起模拟。</summary>
        public bool SelectionMode
        {
            get { return _selectionMode; }
            set
            {
                if (_selectionMode == value) return;
                _selectionMode = value;
                if (_selectionMode) ClearAllKeys();
                RefreshKeyColors();
            }
        }

        public void SetSelection(IList<byte> offsets)
        {
            _selectedHids.Clear();
            if (offsets != null)
                foreach (var o in offsets) _selectedHids.Add(o);
            RefreshKeyColors();
        }

        /// <summary>刷新全部按键的颜色 / 字体，反映“已选中 / 固件不支持”状态。</summary>
        public void RefreshKeyColors()
        {
            foreach (var kv in _keyButtons)
            {
                string key = kv.Key;
                Button btn = kv.Value;
                int hid = GetHidId(key);

                bool supported = IsHidIdSupported(hid);
                bool selected = _selectedHids.Contains(hid);

                if (!_selectionMode)
                {
                    btn.BackColor = BaseColor(key);
                    btn.ForeColor = PickerPalette.Text;
                    btn.Cursor = Cursors.Hand;
                    continue;
                }

                if (!supported)
                {
                    btn.BackColor = PickerPalette.Disabled;
                    btn.ForeColor = PickerPalette.DisabledText;
                    ApplyFont(btn, CalcFontSize(btn.Text, btn.Width), FontStyle.Strikeout);
                    btn.Cursor = Cursors.No;
                    continue;
                }

                btn.ForeColor = PickerPalette.Text;
                ApplyFont(btn, CalcFontSize(btn.Text, btn.Width), FontStyle.Regular);
                btn.Cursor = Cursors.Hand;
                btn.BackColor = selected ? PickerPalette.Selected : BaseColor(key);
            }
        }

        /// <summary>释放本控件持有的字体对象。</summary>
        public void DisposeFonts()
        {
            foreach (var f in _ownedFonts) f.Dispose();
            _ownedFonts.Clear();
            _buttonFontStyle.Clear();
        }

        /// <summary>当前选中的 HID Usage 列表。</summary>
        public List<byte> SelectedOffsets
        {
            get { return _selectedHids.Select(h => (byte)h).ToList(); }
        }
    }
}
