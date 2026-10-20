using System;
using System.Drawing;
using System.Windows.Forms;

namespace KeyboardOG
{
    /// <summary>灯效面板发出的是哪一类改动。</summary>
    public enum LedChangeKind
    {
        Live,       // 效果 / 亮度 / 速度 / 主色 -> CMD_SET_LED
        Fps,        // 刷新率 -> CMD_SET_LED_FPS
        Accent,     // 副色 -> CMD_SET_LED_ACCENT
        Save,       // 保存 -> CMD_SAVE_LED
    }

    /// <summary>一次灯效参数快照（对应固件里的 LEDParams_t，不含 magic）。</summary>
    public sealed class LedSettings
    {
        public byte Effect = 2;         // LED_EFFECT_PRESS
        public byte Brightness = 128;
        public byte Speed = 8;
        public byte Fps = 50;
        public byte R1 = 255, G1 = 0, B1 = 0;
        public byte R2 = 0, G2 = 120, B2 = 255;
    }

    /// <summary>
    /// 灯效设置面板：分组（单色 / 炫彩 / 双色）+ 效果、亮度、速度、刷新率、主色、副色、保存。
    /// 任何改动都立刻通过 Changed 通知主窗体发指令（拖动类控件带 220ms 防抖），
    /// 「保存灯效」才发保存指令。
    /// </summary>
    public class LedPanel : UserControl
    {
        /// <summary>一条效果条目：编号和固件 WS2812BManager.h 的 LED_EFFECT_* 一致。</summary>
        private sealed class EffectEntry
        {
            public readonly byte Id;
            public readonly int Group;      // 0 单色 / 1 炫彩 / 2 双色
            public readonly string Name;

            public EffectEntry(byte id, int group, string name)
            {
                Id = id;
                Group = group;
                Name = name;
            }

            public override string ToString() { return Name; }   // 下拉框直接显示名字
        }

        private static readonly EffectEntry[] Effects =
        {
            // ---- 单色（0~9）----
            new EffectEntry(0,  0, L.T("全灭", "Off")),
            new EffectEntry(1,  0, L.T("常亮", "Solid")),
            new EffectEntry(2,  0, L.T("按下即亮", "Press")),
            new EffectEntry(3,  0, L.T("按下 + 余辉", "Press + fade")),
            new EffectEntry(4,  0, L.T("打字热度", "Typing heat")),
            new EffectEntry(5,  0, L.T("呼吸", "Breath")),
            new EffectEntry(6,  0, L.T("波浪", "Wave")),
            new EffectEntry(7,  0, L.T("星光", "Sparkle")),
            new EffectEntry(8,  0, L.T("涟漪", "Ripple")),
            new EffectEntry(9,  0, L.T("跑马灯", "Comet")),
            // ---- 炫彩（10~14）----
            new EffectEntry(10, 1, L.T("彩虹流动", "Rainbow")),
            new EffectEntry(11, 1, L.T("彩虹呼吸", "Rainbow breath")),
            new EffectEntry(12, 1, L.T("彩虹波浪", "Rainbow wave")),
            new EffectEntry(13, 1, L.T("彩虹星光", "Rainbow sparkle")),
            new EffectEntry(14, 1, L.T("彩虹跑马", "Rainbow comet")),
            // ---- 双色（15~22）----
            new EffectEntry(15, 2, L.T("双色呼吸", "Dual breath")),
            new EffectEntry(16, 2, L.T("双色按下", "Dual press")),
            new EffectEntry(17, 2, L.T("双色按下 + 渐回", "Dual press + fade")),
            new EffectEntry(18, 2, L.T("双色波浪", "Dual wave")),
            new EffectEntry(19, 2, L.T("双色跑马", "Dual comet")),
            new EffectEntry(20, 2, L.T("双色打字", "Dual typing")),
            new EffectEntry(21, 2, L.T("左右渐变", "Left/right gradient")),
            new EffectEntry(22, 2, L.T("双色涟漪", "Dual ripple")),
        };

        private static readonly int[] GroupOfEffect = BuildGroupMap();

        private static int[] BuildGroupMap()
        {
            var map = new int[256];
            foreach (var e in Effects) map[e.Id] = e.Group;
            return map;
        }

        /// <summary>刷新率 10 档（不做随意取值，避免滑块拖动时发一堆不同 fps）。</summary>
        private static readonly byte[] FpsSteps = { 10, 20, 30, 40, 50, 60, 80, 100, 150, 200 };

        public LedSettings Values = new LedSettings();

        /// <summary>从键盘回传（0x0C / 0x0D 两帧）解析灯效参数；没有基本帧就返回 null。</summary>
        public static LedSettings FromFrames(System.Collections.Generic.IEnumerable<byte[]> frames)
        {
            if (frames == null) return null;
            var st = new LedSettings();
            bool hasBasic = false;
            foreach (var f in frames)
            {
                switch (ResponseParser.KindOf(f))
                {
                    case ResponseKind.LedBasic:
                        var b = LedBasicFrame.FromFrame(f);
                        if (b != null)
                        {
                            st.Effect = b.Effect;
                            st.Brightness = b.Brightness;
                            st.Speed = b.Speed;
                            st.Fps = b.Fps;
                            hasBasic = true;
                        }
                        break;
                    case ResponseKind.LedColors:
                        var c = LedColorsFrame.FromFrame(f);
                        if (c != null)
                        {
                            st.R1 = c.R1; st.G1 = c.G1; st.B1 = c.B1;
                            st.R2 = c.R2; st.G2 = c.G2; st.B2 = c.B2;
                        }
                        break;
                }
            }
            return hasBasic ? st : null;
        }

        /// <summary>
        /// 把键盘上的真实灯效参数显示到界面上（不发任何指令）。
        /// 打开灯效页签时先调用它，避免第一次改动就把上位机的默认值写进键盘。
        /// </summary>
        public void ApplyFromDevice(LedSettings s)
        {
            if (s == null) return;

            _suppress = true;
            try
            {
                Values.Effect     = s.Effect;
                Values.Brightness = s.Brightness;
                Values.Speed      = s.Speed;
                Values.Fps        = s.Fps;
                Values.R1 = s.R1; Values.G1 = s.G1; Values.B1 = s.B1;
                Values.R2 = s.R2; Values.G2 = s.G2; Values.B2 = s.B2;

                int group = GroupOfEffect[Values.Effect];
                _cmbGroup.SelectedIndex = group;
                FillEffects(group);
                SelectEffect(Values.Effect);

                int brightness = Values.Brightness;
                if (brightness < _barBrightness.Minimum) brightness = _barBrightness.Minimum;
                if (brightness > _barBrightness.Maximum) brightness = _barBrightness.Maximum;
                _barBrightness.Value = brightness;
                _lblBrightness.Text = Values.Brightness.ToString();

                int speed = Values.Speed;
                if (speed < _barSpeed.Minimum) speed = _barSpeed.Minimum;
                if (speed > _barSpeed.Maximum) speed = _barSpeed.Maximum;
                _barSpeed.Value = speed;
                _lblSpeed.Text = Values.Speed.ToString();

                _barFps.Value = FpsIndex(Values.Fps);
                _lblFps.Text = FpsSteps[_barFps.Value] + " fps";

                RefreshSwatches();
            }
            finally
            {
                _suppress = false;
            }
        }

        /// <summary>改动通知：主窗体据此发对应指令。</summary>
        public event Action<LedChangeKind, LedSettings> Changed;

        private readonly ComboBox _cmbGroup = new ComboBox();
        private readonly ComboBox _cmbEffect = new ComboBox();
        private readonly TrackBar _barBrightness = new TrackBar();
        private readonly TrackBar _barSpeed = new TrackBar();
        private readonly TrackBar _barFps = new TrackBar();
        private readonly Label _lblBrightness = new Label();
        private readonly Label _lblSpeed = new Label();
        private readonly Label _lblFps = new Label();
        private readonly Button _btnColor1 = new Button();
        private readonly Button _btnColor2 = new Button();
        private readonly Button _btnSave = new Button();
        private readonly Label _lblNote = new Label();

        private readonly Timer _debounce = new Timer { Interval = 220 };
        private readonly Timer _debounceFps = new Timer { Interval = 220 };
        private bool _suppress;

        public LedPanel()
        {
            BackColor = SystemColors.Window;
            BuildUi();

            _debounce.Tick += (s, e) =>
            {
                _debounce.Stop();
                Raise(LedChangeKind.Live);
            };
            _debounceFps.Tick += (s, e) =>
            {
                _debounceFps.Stop();
                Raise(LedChangeKind.Fps);
            };
        }

        private void BuildUi()
        {
            _suppress = true;

            var title = new Label
            {
                Text = L.T("灯效设置（六键键盘）", "LED settings (6-key keyboard)"),
                Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold),
                Location = new Point(16, 14),
                Size = new Size(360, 22),
            };

            // ---- 分组 + 效果 ----
            var lblGroup = new Label { Text = L.T("分组：", "Group:"), Location = new Point(16, 50), Size = new Size(52, 20) };
            _cmbGroup.Location = new Point(72, 46);
            _cmbGroup.Size = new Size(150, 24);
            _cmbGroup.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbGroup.Items.Add(L.T("单色", "Single color"));
            _cmbGroup.Items.Add(L.T("炫彩", "Rainbow"));
            _cmbGroup.Items.Add(L.T("双色", "Two colors"));

            var lblEffect = new Label { Text = L.T("效果：", "Effect:"), Location = new Point(16, 86), Size = new Size(52, 20) };
            _cmbEffect.Location = new Point(72, 82);
            _cmbEffect.Size = new Size(230, 24);
            _cmbEffect.DropDownStyle = ComboBoxStyle.DropDownList;

            _cmbGroup.SelectedIndexChanged += (s, e) =>
            {
                if (_suppress) return;
                FillEffects(_cmbGroup.SelectedIndex);
                if (_cmbEffect.Items.Count > 0) _cmbEffect.SelectedIndex = 0;
            };

            _cmbEffect.SelectedIndexChanged += (s, e) =>
            {
                if (_suppress || _cmbEffect.SelectedIndex < 0) return;
                Values.Effect = ((EffectEntry)_cmbEffect.SelectedItem).Id;
                Raise(LedChangeKind.Live);
            };

            // ---- 亮度 ----
            var lblB = new Label { Text = L.T("亮度：", "Brightness:"), Location = new Point(16, 122), Size = new Size(52, 20) };
            _barBrightness.Location = new Point(72, 116);
            _barBrightness.Size = new Size(230, 45);
            _barBrightness.Minimum = 0;
            _barBrightness.Maximum = 255;
            _barBrightness.TickFrequency = 32;
            _barBrightness.SmallChange = 1;
            _barBrightness.LargeChange = 16;
            _barBrightness.Value = Values.Brightness;
            _lblBrightness.Location = new Point(308, 124);
            _lblBrightness.Size = new Size(60, 20);
            _lblBrightness.Text = Values.Brightness.ToString();
            _barBrightness.ValueChanged += (s, e) =>
            {
                if (_suppress) return;
                Values.Brightness = (byte)_barBrightness.Value;
                _lblBrightness.Text = Values.Brightness.ToString();
                Debounced();
            };

            // ---- 速度 ----
            var lblS = new Label { Text = L.T("速度：", "Speed:"), Location = new Point(16, 162), Size = new Size(52, 20) };
            _barSpeed.Location = new Point(72, 156);
            _barSpeed.Size = new Size(230, 45);
            _barSpeed.Minimum = 1;
            _barSpeed.Maximum = 20;
            _barSpeed.TickFrequency = 2;
            _barSpeed.SmallChange = 1;
            _barSpeed.Value = Values.Speed;
            _lblSpeed.Location = new Point(308, 164);
            _lblSpeed.Size = new Size(60, 20);
            _lblSpeed.Text = Values.Speed.ToString();
            _barSpeed.ValueChanged += (s, e) =>
            {
                if (_suppress) return;
                Values.Speed = (byte)_barSpeed.Value;
                _lblSpeed.Text = Values.Speed.ToString();
                Debounced();
            };

            // ---- 刷新率 ----
            var lblF = new Label { Text = L.T("刷新率：", "Frame rate:"), Location = new Point(16, 202), Size = new Size(56, 20) };
            _barFps.Location = new Point(72, 196);
            _barFps.Size = new Size(230, 45);
            _barFps.Minimum = 0;
            _barFps.Maximum = FpsSteps.Length - 1;
            _barFps.TickFrequency = 1;
            _barFps.SmallChange = 1;
            _barFps.LargeChange = 1;
            _barFps.Value = FpsIndex(Values.Fps);
            _lblFps.Location = new Point(308, 204);
            _lblFps.Size = new Size(80, 20);
            _lblFps.Text = FpsSteps[FpsIndex(Values.Fps)] + " fps";
            _barFps.ValueChanged += (s, e) =>
            {
                if (_suppress) return;
                Values.Fps = FpsSteps[_barFps.Value];
                _lblFps.Text = Values.Fps + " fps";
                _debounceFps.Stop();
                _debounceFps.Start();
            };

            // ---- 颜色 ----
            var lblC1 = new Label { Text = L.T("主色：", "Color 1:"), Location = new Point(16, 246), Size = new Size(52, 20) };
            _btnColor1.Location = new Point(72, 242);
            _btnColor1.Size = new Size(74, 28);
            _btnColor1.FlatStyle = FlatStyle.Flat;
            _btnColor1.Click += (s, e) => PickColor(true);

            var lblC2 = new Label { Text = L.T("副色：", "Color 2:"), Location = new Point(170, 246), Size = new Size(52, 20) };
            _btnColor2.Location = new Point(226, 242);
            _btnColor2.Size = new Size(74, 28);
            _btnColor2.FlatStyle = FlatStyle.Flat;
            _btnColor2.Click += (s, e) => PickColor(false);

            _btnSave.Location = new Point(16, 292);
            _btnSave.Size = new Size(150, 38);
            _btnSave.Text = L.T("保存灯效", "Save LED");
            _btnSave.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
            _btnSave.BackColor = Color.FromArgb(210, 232, 255);
            _btnSave.FlatStyle = FlatStyle.Flat;
            _btnSave.FlatAppearance.BorderColor = Color.FromArgb(60, 120, 190);
            _btnSave.UseVisualStyleBackColor = false;
            _btnSave.Click += (s, e) => Raise(LedChangeKind.Save);

            _lblNote.Location = new Point(16, 342);
            _lblNote.Size = new Size(440, 70);
            _lblNote.ForeColor = Color.FromArgb(90, 90, 90);
            _lblNote.Text = L.T(
                "改动会立即生效（滑块拖动结束后发送）。\r\n双色组使用主色 + 副色，炫彩组不使用这两个颜色。\r\n点“保存灯效”才会写进键盘的 EEPROM，断电后仍保留。",
                "Changes apply immediately (sliders send after you stop dragging).\r\nTwo-color effects use color 1 + color 2; rainbow effects ignore them.\r\nPress \"Save LED\" to store them in the keyboard EEPROM.");

            Controls.Add(title);
            Controls.Add(lblGroup); Controls.Add(_cmbGroup);
            Controls.Add(lblEffect); Controls.Add(_cmbEffect);
            Controls.Add(lblB); Controls.Add(_barBrightness); Controls.Add(_lblBrightness);
            Controls.Add(lblS); Controls.Add(_barSpeed); Controls.Add(_lblSpeed);
            Controls.Add(lblF); Controls.Add(_barFps); Controls.Add(_lblFps);
            Controls.Add(lblC1); Controls.Add(_btnColor1);
            Controls.Add(lblC2); Controls.Add(_btnColor2);
            Controls.Add(_btnSave);
            Controls.Add(_lblNote);

            int group = GroupOfEffect[Values.Effect];
            _cmbGroup.SelectedIndex = group;
            FillEffects(group);
            SelectEffect(Values.Effect);
            RefreshSwatches();

            _suppress = false;
        }

        /// <summary>fps 值 -> 档位下标（找不到就用最接近的档）。</summary>
        private static int FpsIndex(byte fps)
        {
            int best = 0;
            int bestDiff = 255;
            for (int i = 0; i < FpsSteps.Length; i++)
            {
                int diff = Math.Abs(FpsSteps[i] - fps);
                if (diff < bestDiff) { bestDiff = diff; best = i; }
            }
            return best;
        }

        private void FillEffects(int group)
        {
            _cmbEffect.Items.Clear();
            foreach (var e in Effects)
            {
                if (e.Group == group) _cmbEffect.Items.Add(e);
            }
        }

        private void SelectEffect(byte id)
        {
            for (int i = 0; i < _cmbEffect.Items.Count; i++)
            {
                if (((EffectEntry)_cmbEffect.Items[i]).Id == id)
                {
                    _cmbEffect.SelectedIndex = i;
                    return;
                }
            }
            if (_cmbEffect.Items.Count > 0) _cmbEffect.SelectedIndex = 0;
        }

        private void Debounced()
        {
            _debounce.Stop();
            _debounce.Start();
        }

        private void PickColor(bool primary)
        {
            byte r = primary ? Values.R1 : Values.R2;
            byte g = primary ? Values.G1 : Values.G2;
            byte b = primary ? Values.B1 : Values.B2;

            using (var dlg = new ColorDialog { Color = Color.FromArgb(r, g, b), FullOpen = true })
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                if (primary) { Values.R1 = dlg.Color.R; Values.G1 = dlg.Color.G; Values.B1 = dlg.Color.B; }
                else { Values.R2 = dlg.Color.R; Values.G2 = dlg.Color.G; Values.B2 = dlg.Color.B; }
            }
            RefreshSwatches();
            Raise(primary ? LedChangeKind.Live : LedChangeKind.Accent);
        }

        private void RefreshSwatches()
        {
            _btnColor1.BackColor = Color.FromArgb(Values.R1, Values.G1, Values.B1);
            _btnColor2.BackColor = Color.FromArgb(Values.R2, Values.G2, Values.B2);
            _btnColor1.Text = string.Format("#{0:X2}{1:X2}{2:X2}", Values.R1, Values.G1, Values.B1);
            _btnColor2.Text = string.Format("#{0:X2}{1:X2}{2:X2}", Values.R2, Values.G2, Values.B2);
            _btnColor1.ForeColor = IsDark(_btnColor1.BackColor) ? Color.White : Color.Black;
            _btnColor2.ForeColor = IsDark(_btnColor2.BackColor) ? Color.White : Color.Black;
        }

        private static bool IsDark(Color c)
        {
            return (c.R * 299 + c.G * 587 + c.B * 114) / 1000 < 128;
        }

        private void Raise(LedChangeKind kind)
        {
            var handler = Changed;
            if (handler != null) handler(kind, Values);
        }
    }
}
