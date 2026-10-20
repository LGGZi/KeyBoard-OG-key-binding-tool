using System;
using System.Drawing;
using System.Windows.Forms;

namespace KeyboardOG
{
    /// <summary>灯效面板发出的是哪一类改动。</summary>
    public enum LedChangeKind
    {
        Live,       // 效果 / 亮度 / 速度 / 主色 -> CMD_SET_LED
        Accent,     // 副色 -> CMD_SET_LED_ACCENT
        Save,       // 保存 -> CMD_SAVE_LED
    }

    /// <summary>一次灯效参数快照（和固件里的 LEDParams_t 对应，不含 magic）。</summary>
    public sealed class LedSettings
    {
        public byte Effect = 2;         // LED_EFFECT_PRESS
        public byte Brightness = 128;
        public byte Speed = 8;
        public byte R1 = 255, G1 = 0, B1 = 0;
        public byte R2 = 0, G2 = 120, B2 = 255;
    }

    /// <summary>
    /// 灯效设置面板：模式（效果）、亮度、速度、主色、副色、保存。
    /// 任何一项改动都会立刻通过 Changed 事件通知主窗体发指令（滑块带 200ms 防抖），
    /// 「保存灯效」按钮才发保存指令。
    /// </summary>
    public class LedPanel : UserControl
    {
        //效果编号和固件 WS2812BManager.h 里的 LED_EFFECT_* 一一对应
        private static readonly string[] EffectNames =
        {
            L.T("全灭", "Off"),
            L.T("常亮", "Solid"),
            L.T("按下即亮", "Press"),
            L.T("按下 + 余辉", "Press + fade"),
            L.T("打字热度", "Typing heat"),
            L.T("彩虹", "Rainbow"),
            L.T("呼吸", "Breath"),
            L.T("波浪", "Wave"),
            L.T("星光", "Sparkle"),
            L.T("涟漪", "Ripple"),
            L.T("跑马灯", "Comet"),
            L.T("双色呼吸", "Dual breath"),
        };

        public LedSettings Values = new LedSettings();

        /// <summary>改动通知：主窗体据此发对应指令。</summary>
        public event Action<LedChangeKind, LedSettings> Changed;

        private readonly ComboBox _cmbEffect = new ComboBox();
        private readonly TrackBar _barBrightness = new TrackBar();
        private readonly TrackBar _barSpeed = new TrackBar();
        private readonly Label _lblBrightness = new Label();
        private readonly Label _lblSpeed = new Label();
        private readonly Button _btnColor1 = new Button();
        private readonly Button _btnColor2 = new Button();
        private readonly Button _btnSave = new Button();
        private readonly Label _lblNote = new Label();

        private readonly Timer _debounce = new Timer { Interval = 220 };
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
        }

        private void BuildUi()
        {
            _suppress = true;      // 初始化时不要触发 Changed
            var title = new Label
            {
                Text = L.T("灯效设置（六键键盘）", "LED settings (6-key keyboard)"),
                Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold),
                Location = new Point(16, 14),
                Size = new Size(360, 22),
            };

            var lblEffect = new Label { Text = L.T("模式：", "Mode:"), Location = new Point(16, 50), Size = new Size(52, 20) };
            _cmbEffect.Location = new Point(72, 46);
            _cmbEffect.Size = new Size(230, 24);
            _cmbEffect.DropDownStyle = ComboBoxStyle.DropDownList;
            for (int i = 0; i < EffectNames.Length; i++)
                _cmbEffect.Items.Add(string.Format("{0}  {1}", i, EffectNames[i]));
            _cmbEffect.SelectedIndex = Values.Effect;
            _cmbEffect.SelectedIndexChanged += (s, e) =>
            {
                if (_suppress || _cmbEffect.SelectedIndex < 0) return;
                Values.Effect = (byte)_cmbEffect.SelectedIndex;
                Raise(LedChangeKind.Live);
            };

            var lblBrightnessName = new Label { Text = L.T("亮度：", "Brightness:"), Location = new Point(16, 90), Size = new Size(52, 20) };
            _barBrightness.Location = new Point(72, 84);
            _barBrightness.Size = new Size(230, 45);
            _barBrightness.Minimum = 0;
            _barBrightness.Maximum = 255;
            _barBrightness.TickFrequency = 32;
            _barBrightness.SmallChange = 1;
            _barBrightness.LargeChange = 16;
            _barBrightness.Value = Values.Brightness;
            _lblBrightness.Location = new Point(308, 92);
            _lblBrightness.Size = new Size(60, 20);
            _lblBrightness.Text = Values.Brightness.ToString();
            _barBrightness.ValueChanged += (s, e) =>
            {
                if (_suppress) return;
                Values.Brightness = (byte)_barBrightness.Value;
                _lblBrightness.Text = Values.Brightness.ToString();
                Debounced();
            };

            var lblSpeedName = new Label { Text = L.T("速度：", "Speed:"), Location = new Point(16, 136), Size = new Size(52, 20) };
            _barSpeed.Location = new Point(72, 130);
            _barSpeed.Size = new Size(230, 45);
            _barSpeed.Minimum = 1;
            _barSpeed.Maximum = 20;
            _barSpeed.TickFrequency = 2;
            _barSpeed.SmallChange = 1;
            _barSpeed.Value = Values.Speed;
            _lblSpeed.Location = new Point(308, 138);
            _lblSpeed.Size = new Size(60, 20);
            _lblSpeed.Text = Values.Speed.ToString();
            _barSpeed.ValueChanged += (s, e) =>
            {
                if (_suppress) return;
                Values.Speed = (byte)_barSpeed.Value;
                _lblSpeed.Text = Values.Speed.ToString();
                Debounced();
            };

            var lblColor1 = new Label { Text = L.T("主色：", "Color 1:"), Location = new Point(16, 186), Size = new Size(52, 20) };
            _btnColor1.Location = new Point(72, 182);
            _btnColor1.Size = new Size(74, 28);
            _btnColor1.FlatStyle = FlatStyle.Flat;
            _btnColor1.Click += (s, e) => PickColor(true);

            var lblColor2 = new Label { Text = L.T("副色：", "Color 2:"), Location = new Point(170, 186), Size = new Size(52, 20) };
            _btnColor2.Location = new Point(226, 182);
            _btnColor2.Size = new Size(74, 28);
            _btnColor2.FlatStyle = FlatStyle.Flat;
            _btnColor2.Click += (s, e) => PickColor(false);

            _btnSave.Location = new Point(16, 232);
            _btnSave.Size = new Size(150, 38);
            _btnSave.Text = L.T("保存灯效", "Save LED");
            _btnSave.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
            _btnSave.BackColor = Color.FromArgb(210, 232, 255);
            _btnSave.FlatStyle = FlatStyle.Flat;
            _btnSave.FlatAppearance.BorderColor = Color.FromArgb(60, 120, 190);
            _btnSave.UseVisualStyleBackColor = false;
            _btnSave.Click += (s, e) => Raise(LedChangeKind.Save);

            _lblNote.Location = new Point(16, 282);
            _lblNote.Size = new Size(420, 60);
            _lblNote.ForeColor = Color.FromArgb(90, 90, 90);
            _lblNote.Text = L.T(
                "改动会立即生效（滑块拖动结束后发送）；\r\n点“保存灯效”才会写进键盘的 EEPROM，断电后仍保留。",
                "Changes apply immediately (sliders send after you stop dragging).\r\nPress \"Save LED\" to store them in the keyboard EEPROM.");

            Controls.Add(title);
            Controls.Add(lblEffect);
            Controls.Add(_cmbEffect);
            Controls.Add(lblBrightnessName);
            Controls.Add(_barBrightness);
            Controls.Add(_lblBrightness);
            Controls.Add(lblSpeedName);
            Controls.Add(_barSpeed);
            Controls.Add(_lblSpeed);
            Controls.Add(lblColor1);
            Controls.Add(_btnColor1);
            Controls.Add(lblColor2);
            Controls.Add(_btnColor2);
            Controls.Add(_btnSave);
            Controls.Add(_lblNote);

            RefreshSwatches();
            _suppress = false;
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
