using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Web.Script.Serialization;

namespace KLEconvert
{
    /// <summary>一个按键：来自 KLE raw 数据，保留它在原始行数组里的位置以便回写。</summary>
    public class KeyItem
    {
        public object[] Row;          // 所属行的 JSON 数组
        public int Element;           // 在该行中的下标
        public List<string> Labels = new List<string>();

        public double X, Y, W = 1.0, H = 1.0;
        public double R, RX, RY;

        public int Index = -1;        // -1 = 未编号
        public int Type;              // 0 普通 / 1 修饰 / 2 编码器 / 3 装饰

        public bool Rotated { get { return Math.Abs(R) > 1e-9; } }

        /// <summary>去掉 #N 行之后的第一行文字，仅用于显示与注释。</summary>
        public string Name
        {
            get
            {
                foreach (string line in Labels)
                {
                    if (IsNumberLine(line)) continue;
                    if (line.Trim().Length > 0) return line.Trim();
                }
                return string.Empty;
            }
        }

        public int Ux { get { return ToUnits(X); } }
        public int Uy { get { return ToUnits(Y); } }
        public int Uw { get { return ToUnits(W); } }
        public int Uh { get { return ToUnits(H); } }

        public static bool IsNumberLine(string line)
        {
            if (line == null) return false;
            string s = line.Trim();
            if (s.Length < 2 || s[0] != '#') return false;
            for (int i = 1; i < s.Length; i++)
                if (!char.IsDigit(s[i])) return false;
            return true;
        }

        public static int ToUnits(double v)
        {
            return (int)Math.Round(v * 4.0, MidpointRounding.AwayFromZero);
        }

        /// <summary>用编号重写标签行（编号放在第一行，原有文字保留在后面）。</summary>
        public List<string> BuildLabels(bool includeNumber)
        {
            var lines = new List<string>();
            foreach (string line in Labels)
                if (!IsNumberLine(line)) lines.Add(line);

            // 去掉尾部多余空行，但保留全空（表示无文字）
            while (lines.Count > 0 && lines[lines.Count - 1].Trim().Length == 0) lines.RemoveAt(lines.Count - 1);

            if (includeNumber && Index >= 0) lines.Insert(0, "#" + Index.ToString(CultureInfo.InvariantCulture));
            if (lines.Count == 0) lines.Add(string.Empty);
            return lines;
        }
    }

    /// <summary>KLE 原始数据 + 解析结果 + 导出（JSON / .h）。</summary>
    public class KleLayout
    {
        public object Root;                      // object[]（通常是行数组）
        public List<KeyItem> Keys = new List<KeyItem>();
        public List<string> Warnings = new List<string>();
        public string SourceName = "";

        public double WidthU, HeightU;
        public int UnitWidth, UnitHeight;

        public int NumberedCount
        {
            get
            {
                int n = 0;
                foreach (var k in Keys) if (k.Index >= 0) n++;
                return n;
            }
        }

        private static readonly JavaScriptSerializer Ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public static KleLayout Parse(string json)
        {
            object root = Ser.DeserializeObject(json);
            var layout = new KleLayout();
            layout.Root = root;

            object rowsObj = root;
            var dict = root as Dictionary<string, object>;
            if (dict != null)
            {
                object v;
                if (dict.TryGetValue("keys", out v)) rowsObj = v;
                else if (dict.TryGetValue("layout", out v)) rowsObj = v;
                else rowsObj = new object[] { root };
            }

            var rows = rowsObj as object[];
            if (rows == null) throw new FormatException("不是 KLE Raw data：顶层应为数组");

            var meta = new Dictionary<string, double>();
            meta["r"] = 0; meta["rx"] = 0; meta["ry"] = 0;

            for (int rowIndex = 0; rowIndex < rows.Length; rowIndex++)
            {
                var metaObj = rows[rowIndex] as Dictionary<string, object>;
                if (metaObj != null)
                {
                    foreach (string k in new[] { "r", "rx", "ry" })
                        if (metaObj.ContainsKey(k)) meta[k] = Num(metaObj[k], 0);
                    continue;
                }

                var row = rows[rowIndex] as object[];
                if (row == null) continue;

                double xCursor = 0;
                var pending = new Dictionary<string, object>();

                for (int i = 0; i < row.Length; i++)
                {
                    var obj = row[i] as Dictionary<string, object>;
                    if (obj != null)
                    {
                        foreach (string k in new[] { "x", "y", "w", "h", "r", "rx", "ry", "name", "c", "t", "a" })
                            if (obj.ContainsKey(k)) pending[k] = obj[k];
                        foreach (string k in new[] { "r", "rx", "ry" })
                            if (obj.ContainsKey(k)) meta[k] = Num(obj[k], 0);
                        continue;
                    }

                    string label = row[i] == null ? "" : row[i].ToString();
                    var key = new KeyItem();
                    key.Row = row;
                    key.Element = i;
                    key.Labels = SplitLabels(label);

                    key.W = PendingNum(pending, "w", 1.0);
                    key.H = PendingNum(pending, "h", 1.0);
                    key.X = xCursor + PendingNum(pending, "x", 0.0);
                    key.Y = rowIndex + PendingNum(pending, "y", 0.0);
                    key.R = PendingNum(pending, "r", meta["r"]);
                    key.RX = PendingNum(pending, "rx", meta["rx"]);
                    key.RY = PendingNum(pending, "ry", meta["ry"]);

                    // 标签里的 #N
                    for (int n = 0; n < key.Labels.Count; n++)
                    {
                        string line = key.Labels[n].Trim();
                        if (!KeyItem.IsNumberLine(line)) continue;
                        int idx;
                        if (int.TryParse(line.Substring(1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out idx)
                            && key.Index < 0)
                            key.Index = idx;
                    }

                    layout.Keys.Add(key);
                    xCursor = key.X + key.W;
                    pending.Clear();
                }
            }

            layout.Measure();
            return layout;
        }

        private static List<string> SplitLabels(string label)
        {
            if (label == null) return new List<string> { string.Empty };
            return new List<string>(label.Replace("\r\n", "\n").Split('\n'));
        }

        private static double PendingNum(Dictionary<string, object> pending, string key, double fallback)
        {
            object v;
            if (!pending.TryGetValue(key, out v)) return fallback;
            return Num(v, fallback);
        }

        private static double Num(object v, double fallback)
        {
            if (v == null) return fallback;
            if (v is double) return (double)v;
            if (v is int) return (int)v;
            if (v is long) return (long)v;
            if (v is decimal) return (double)(decimal)v;
            if (v is string)
            {
                double d;
                if (double.TryParse((string)v, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
            }
            return fallback;
        }

        public void Measure()
        {
            int maxX = 0, maxY = 0;
            foreach (var k in Keys)
            {
                if (k.Ux + k.Uw > maxX) maxX = k.Ux + k.Uw;
                if (k.Uy + k.Uh > maxY) maxY = k.Uy + k.Uh;
            }
            UnitWidth = maxX;
            UnitHeight = maxY;
            WidthU = maxX / 4.0;
            HeightU = maxY / 4.0;
        }

        /// <summary>按「从上到下、从左到右」一键编号。</summary>
        public void AutoNumber()
        {
            var ordered = new List<KeyItem>(Keys);
            ordered.Sort(delegate (KeyItem a, KeyItem b)
            {
                int c = a.Uy.CompareTo(b.Uy);
                if (c != 0) return c;
                return a.Ux.CompareTo(b.Ux);
            });
            for (int i = 0; i < ordered.Count; i++) ordered[i].Index = i;
        }

        public void ClearNumbers()
        {
            foreach (var k in Keys) k.Index = -1;
        }

        /// <summary>把编号写回原始 JSON 并序列化。</summary>
        public string ToJson(bool includeNumbers)
        {
            foreach (var k in Keys)
            {
                var labels = k.BuildLabels(includeNumbers);
                k.Row[k.Element] = string.Join("\n", labels.ToArray());
            }
            return Ser.Serialize(Root);
        }

        /// <summary>生成固件用的 .h（数组顺序：全部编号则按编号，否则按位置）。</summary>
        public string ToHeader(string arrayName, bool includeNumbers)
        {
            bool allNumbered = Keys.Count > 0 && NumberedCount == Keys.Count;

            var ordered = new List<KeyItem>(Keys);
            if (allNumbered && includeNumbers)
            {
                ordered.Sort(delegate (KeyItem a, KeyItem b) { return a.Index.CompareTo(b.Index); });
            }
            else
            {
                ordered.Sort(delegate (KeyItem a, KeyItem b)
                {
                    int c = a.Uy.CompareTo(b.Uy);
                    if (c != 0) return c;
                    return a.Ux.CompareTo(b.Ux);
                });
            }

            var sb = new StringBuilder();
            sb.AppendLine("/* Generated by KLEconvert from " + SourceName + " on " + DateTime.Now.ToString("yyyy-MM-dd") + ".");
            sb.AppendLine(" * " + Keys.Count + " keys, bounding box " + WidthU.ToString("0.00", CultureInfo.InvariantCulture)
                          + "u x " + HeightU.ToString("0.00", CultureInfo.InvariantCulture) + "u ("
                          + UnitWidth + " x " + UnitHeight + " units of 0.25u).");
            if (allNumbered && includeNumbers)
                sb.AppendLine(" * Key index: from #N in key labels.");
            else
                sb.AppendLine(" * Order: top to bottom, left to right. Keep key_map indices identical.");
            sb.AppendLine(" * Paste the array below into LayoutManager.h (replace layout_custom).");
            sb.AppendLine(" */");
            sb.AppendLine("const KeyLayoutItem " + arrayName + "[] PROGMEM = {");

            for (int i = 0; i < ordered.Count; i++)
            {
                var k = ordered[i];
                int flags = (k.Rotated ? 0xC0 : 0) | (k.Type & 0x3F);
                string label = (k.Name ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
                if (label.Length > 10) label = label.Substring(0, 10);

                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "    {{ {0,3}, {1,3}, {2,3}, {3,3}, 0x{4:X2} }},  /* {5,3}: {6,-10} x={7:0.00}u y={8:0.00}u w={9:0.00}u h={10:0.00}u{11} */\r\n",
                    k.Ux, k.Uy, k.Uw, k.Uh, flags, i, label,
                    k.X, k.Y, k.W, k.H,
                    k.Rotated ? string.Format(CultureInfo.InvariantCulture, "  ROT {0:0.0}deg", k.R) : string.Empty);
            }

            sb.AppendLine("};");
            sb.AppendLine("#define " + arrayName.ToUpperInvariant() + "_COUNT (sizeof(" + arrayName + ") / sizeof(" + arrayName + "[0]))");
            return sb.ToString();
        }
    }
}
