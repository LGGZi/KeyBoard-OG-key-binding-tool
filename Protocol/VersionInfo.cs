using System;
using System.Collections.Generic;

namespace KeyboardOG
{
    /// <summary>
    /// 固件 / 软件版本号。
    ///
    /// 固件把一个字节按 **2 | 3 | 3** 位切成三段（见固件 HIDcmdHandler.h 的注释）：
    ///   · 前 2 位 = 小数点前整数
    ///   · 中间 3 位 = 小数点后第一位
    ///   · 最后 3 位 = 小数点后第二位
    ///
    /// 例子：0b01_011_001 = 0x59 = 1.31     0b01_000_001 = 0x41 = 1.01
    /// 所以显示出来就是 "1.31" 这种两段小数（不是 1.3.1）。
    /// </summary>
    public struct FirmwareVersion : IComparable<FirmwareVersion>, IEquatable<FirmwareVersion>
    {
        public int Major;     // 0 ~ 3
        public int Minor1;    // 0 ~ 7
        public int Minor2;    // 0 ~ 7

        public static FirmwareVersion FromByte(byte raw)
        {
            return new FirmwareVersion
            {
                Major = (raw >> 6) & 0x03,
                Minor1 = (raw >> 3) & 0x07,
                Minor2 = raw & 0x07,
            };
        }

        public static FirmwareVersion Of(int major, int minor1, int minor2)
        {
            return new FirmwareVersion { Major = major, Minor1 = minor1, Minor2 = minor2 };
        }

        public byte ToByte()
        {
            return (byte)(((Major & 0x03) << 6) | ((Minor1 & 0x07) << 3) | (Minor2 & 0x07));
        }

        /// <summary>比较大小 —— 按"整数位、小数第一位、小数第二位"逐段比。</summary>
        public int CompareTo(FirmwareVersion o)
        {
            if (Major != o.Major) return Major.CompareTo(o.Major);
            if (Minor1 != o.Minor1) return Minor1.CompareTo(o.Minor1);
            return Minor2.CompareTo(o.Minor2);
        }

        public bool Equals(FirmwareVersion o) { return CompareTo(o) == 0; }
        public override bool Equals(object obj) { return obj is FirmwareVersion && Equals((FirmwareVersion)obj); }
        public override int GetHashCode() { return ToByte(); }

        /// <summary>显示成 1.31 / 1.01 这样（小数点后两位）。</summary>
        public override string ToString()
        {
            return Major.ToString() + "." + Minor1.ToString() + Minor2.ToString();
        }
    }

    /// <summary>本上位机软件的版本号（和固件同一套格式，便于对照）。</summary>
    public static class AppVersion
    {
        // 版本号只有一个来源：AssemblyInfo.cs 的 AssemblyVersion（1.10.0.0 -> 1.10）。
        // 这里不再手写常量，避免"程序里写着 1.02、文件属性是 1.07"这种不一致。
        public static readonly FirmwareVersion Current = FromAssembly();

        private static FirmwareVersion FromAssembly()
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            int minor2 = v.Build & 0x07;      // 2|3|3 只有 3 位给最后一段
            return FirmwareVersion.Of(v.Major & 0x03, v.Minor & 0x07, minor2);
        }

        public static string Text { get { return Current.ToString(); } }
    }

    /// <summary>固件版本相对本软件的支持状态。</summary>
    public enum FirmwareSupport
    {
        Supported = 0,
        TooOld = 1,      // 固件太旧，让用户更新固件
        TooNew = 2,      // 固件太新，让用户更新软件
    }

    /// <summary>
    /// 「软件版本 ↔ 支持的固件版本范围」对应表。
    ///
    /// 判定规则：拿本软件的版本号在表里找到它那一行，再看固件版本是否落在 [Min, Max] 里；
    /// 小于 Min 就是"固件过老"，大于 Max 就是"固件过新"。
    /// 表里没有本软件的版本时，用整张表的范围当兜底（至少能说清是偏旧还是偏新）。
    /// </summary>
    /// <summary>
    /// 按功能判断固件是否支持 —— 比"软件 ↔ 固件范围表"更适合新增功能：
    /// 老固件照常能用（只是没有新功能），新功能单独卡版本。
    /// </summary>
    public static class FeatureGate
    {
        /// <summary>粘滞键 / 连发（behavior 高 4 位 + 连发参数区）从固件 1.10 开始有。</summary>
        public static readonly FirmwareVersion MinForBehavior = FirmwareVersion.Of(1, 1, 0);

        public static bool SupportsBehavior(FirmwareVersion firmware)
        {
            return firmware.CompareTo(MinForBehavior) >= 0;
        }
    }

    public static class Compatibility
    {
        public sealed class Entry
        {
            public FirmwareVersion Software;
            public FirmwareVersion MinFirmware;
            public FirmwareVersion MaxFirmware;
            public string Note = string.Empty;
        }

        // ---- 对应状态列表：一行 = 一个软件版本支持哪个固件范围 ----
        // 发新软件版本时往上加一行（越靠前越新）。
        private static readonly Entry[] Table =
        {
            // 软件 1.10：支持固件 1.10 ~ 1.57（粘滞键 / 连发：behavior 高 4 位 + 连发参数区）
            // 1.10 以前没有 behavior，新软件直接按“固件版本过老”提示，不再兼容
            // 注：版本号和固件用同一套 2|3|3 编码，最后一段只有 3 位，写 1.0.8.0 会被截成 1.00
            new Entry
            {
                Software    = FirmwareVersion.Of(1, 1, 0),
                MinFirmware = FirmwareVersion.Of(1, 1, 0),
                MaxFirmware = FirmwareVersion.Of(1, 5, 7),
                Note        = "sticky keys / auto repeat (behaviour nibble, repeat params in EEPROM)",
            },
            // 软件 1.07：支持固件 1.00 ~ 1.57（布局上传 / 灯效指令 / 灯效查询）
            new Entry
            {
                Software    = FirmwareVersion.Of(1, 0, 7),
                MinFirmware = FirmwareVersion.Of(1, 0, 0),
                MaxFirmware = FirmwareVersion.Of(1, 5, 7),
                Note        = "layout upload, LED settings and LED query",
            },
            // 软件 1.02：支持固件 1.00 ~ 1.57（1.xx 这一代协议）
            new Entry
            {
                Software    = FirmwareVersion.Of(1, 0, 2),
                MinFirmware = FirmwareVersion.Of(1, 0, 0),
                MaxFirmware = FirmwareVersion.Of(1, 5, 7),
                Note        = "current mapping/parameter protocol (APPEND/OVERRIDE/CLEAR, device info carries the version)",
            },
        };

        public static IList<Entry> All { get { return Table; } }

        /// <summary>找到本软件版本对应的一行（找不到返回 null）。</summary>
        public static Entry RowFor(FirmwareVersion software)
        {
            foreach (var e in Table)
                if (e.Software.Equals(software)) return e;
            return null;
        }

        /// <summary>判断某个固件版本在不在支持范围内。</summary>
        public static FirmwareSupport Check(FirmwareVersion firmware)
        {
            return Check(firmware, AppVersion.Current);
        }

        public static FirmwareSupport Check(FirmwareVersion firmware, FirmwareVersion software)
        {
            var row = RowFor(software);
            if (row != null)
            {
                if (firmware.CompareTo(row.MinFirmware) < 0) return FirmwareSupport.TooOld;
                if (firmware.CompareTo(row.MaxFirmware) > 0) return FirmwareSupport.TooNew;
                return FirmwareSupport.Supported;
            }

            FirmwareVersion lo = Table[Table.Length - 1].MinFirmware;
            FirmwareVersion hi = Table[0].MaxFirmware;
            foreach (var e in Table)
            {
                if (e.MinFirmware.CompareTo(lo) < 0) lo = e.MinFirmware;
                if (e.MaxFirmware.CompareTo(hi) > 0) hi = e.MaxFirmware;
            }
            if (firmware.CompareTo(lo) < 0) return FirmwareSupport.TooOld;
            if (firmware.CompareTo(hi) > 0) return FirmwareSupport.TooNew;
            return FirmwareSupport.Supported;
        }

        /// <summary>给日志/提示用的一句话描述（中文原文，界面按当前语言翻译）。</summary>
        public static string DescribeRange(FirmwareVersion software)
        {
            var row = RowFor(software);
            if (row == null) return string.Empty;
            return string.Format("支持固件 {0} ~ {1}", row.MinFirmware, row.MaxFirmware);
        }
    }
}
