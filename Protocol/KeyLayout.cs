using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace KeyboardOG
{
    /// <summary>
    /// 键盘布局：由固件逐键发来的 0xFE 帧组成（每键一帧）。
    ///
    /// 坐标单位 0.25u（4 = 1u），左上角为原点，x 向右、y 向下。
    /// 布局可能不完整（超时 / 丢帧 / 索引对不上）—— 这时 Valid 为 false，
    /// 上层应回退到固定网格，并把 Error 记到指令日志里。
    /// </summary>
    public sealed class KeyLayout
    {
        public readonly List<KeyLayoutItem> Keys = new List<KeyLayoutItem>();

        /// <summary>包围盒（0.25u 单位）。</summary>
        public int WidthUnits { get; private set; }
        public int HeightUnits { get; private set; }

        public float WidthU { get { return WidthUnits / (float)KeyLayoutItem.UnitsPerU; } }
        public float HeightU { get { return HeightUnits / (float)KeyLayoutItem.UnitsPerU; } }

        /// <summary>是否可用（帧数、索引、尺寸都校验通过）。</summary>
        public bool Valid { get; private set; }

        /// <summary>校验失败的原因；Valid 为 true 时是 null。</summary>
        public string Error { get; private set; }

        public int Count { get { return Keys.Count; } }

        /// <summary>按键数不相符时，多余的帧被丢弃、缺失的键没有条目。</summary>
        public bool HasKey(int index) { return Get(index) != null; }

        public KeyLayoutItem Get(int index)
        {
            if (index < 0) return null;
            foreach (var k in Keys)
                if (k.Index == index) return k;
            return null;
        }

        /// <summary>
        /// 从一批回传帧里取出布局。
        /// expectedCount 是设备信息里的 BUTTONCOUNT：必须一一对应，否则整次作废。
        /// </summary>
        public static KeyLayout FromFrames(IEnumerable<byte[]> frames, int expectedCount, out string error)
        {
            error = null;
            var layout = new KeyLayout();
            if (frames == null)
            {
                error = L.T("没有收到任何布局帧", "No layout frames received");
                return null;
            }

            foreach (var f in frames)
            {
                if (ResponseParser.KindOf(f) != ResponseKind.LayoutItem) continue;
                var item = KeyLayoutItem.FromFrame(f);
                if (item != null) layout.Keys.Add(item);
            }

            if (layout.Keys.Count == 0)
            {
                error = L.T("没有收到布局帧", "No layout frames received");
                return null;
            }

            if (layout.Keys.Count != expectedCount)
            {
                error = L.F("布局帧数量 {0} 与按键数 {1} 不一致",
                    "Got {0} layout frames but {1} keys", layout.Keys.Count, expectedCount);
                return null;
            }

            var seen = new bool[expectedCount];
            foreach (var k in layout.Keys)
            {
                if (k.Index < 0 || k.Index >= expectedCount)
                {
                    error = L.F("布局帧里的按键索引越界：{0}", "Layout frame index out of range: {0}", k.Index);
                    return null;
                }
                if (seen[k.Index])
                {
                    error = L.F("布局帧里的按键索引重复：{0}", "Duplicate layout frame index: {0}", k.Index);
                    return null;
                }
                seen[k.Index] = true;

                if (k.W < 1 || k.H < 1)
                {
                    error = L.F("按键 {0} 的宽高无效（{1}×{2}）",
                        "Key {0} has invalid size ({1}×{2})", k.Index, k.W, k.H);
                    return null;
                }
            }

            for (int i = 0; i < expectedCount; i++)
            {
                if (!seen[i])
                {
                    error = L.F("布局缺少按键 {0}", "Layout is missing key {0}", i);
                    return null;
                }
            }

            int maxX = 0, maxY = 0;
            foreach (var k in layout.Keys)
            {
                if (k.X + k.W > maxX) maxX = k.X + k.W;
                if (k.Y + k.H > maxY) maxY = k.Y + k.H;
            }
            layout.WidthUnits = maxX;
            layout.HeightUnits = maxY;
            layout.Valid = true;
            return layout;
        }

        public override string ToString()
        {
            return L.F("{0} 键 · {1:0.00}u × {2:0.00}u", "{0} keys · {1:0.00}u × {2:0.00}u",
                Keys.Count, WidthU, HeightU);
        }
    }
}
