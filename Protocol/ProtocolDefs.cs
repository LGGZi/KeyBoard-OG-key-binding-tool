using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KeyboardOG
{
    // ============================================================
    //  与固件 HIDcmdHandler.h / Universal.h 对应的协议常量
    // ============================================================
    //  8 字节指令（不含 Report ID）布局：
    //
    //   idx  SET_MAP          SET_PARAM        CALIBRATE      QUERY
    //   [0]  CMD=0x01         CMD=0x02         CMD=0x03       CMD=0x04
    //   [1]  device           parameter        mode           全部/单个
    //   [2]  key(0-13)        key(0-13)        key(0-13)      key(0-13)
    //   [3]  offset           value 高字节     -              value 高字节
    //   [4]  operation        value 低字节     -              查询目标
    //   [5]  storageIndex     0                -              value 低字节
    //   [6]  0                0                0              0
    //   [7]  序号(见下)       序号             序号           序号
    //
    //  [7] 是固件未使用的保留字节，上位机写入一个自增序号。
    //  原因：固件 loop() 里调用 HIDcmdResponseIfChanged()，它会 memcmp 上一条
    //  指令，只有内容不同才执行。连续发送完全相同的两条指令，第二条会被丢弃。
    //  序号保证每条指令的 8 字节都不同，重复操作（例如同一个键追加两次同样的
    //  映射、连续两次校准）才不会被固件吞掉。
    // ============================================================

    public enum HidCmd : byte
    {
        SetMap    = 0x01,
        SetParam  = 0x02,
        Calibrate = 0x03,
        Query     = 0x04,
        Ack       = 0x05,
        Fin       = 0x06,
        Refresh   = 0x07,
        SetLed    = 0x08,   // 即时设置灯效：[1]=效果 [2]=亮度 [3]=速度 [4..6]=主色
        SetLedAccent = 0x09,// 即时设置副色：[1..3]=RGB
        SaveLed   = 0x0A,   // 保存灯效参数：[1]=0x5A 确认
        SetLedFps = 0x0B,   // 设置灯效刷新率：[1]=fps（10~200）
    }

    /// <summary>
    /// CMD_REFRESH 的 [1] 字节：让下位机把 EEPROM 重新载入内存，
    /// 这样改完映射 / 参数不用重新插拔就生效。
    /// </summary>
    public enum RefreshTarget : byte
    {
        Mapping   = 0x00,   // keyCodeInit()
        Parameter = 0x01,   // keyParameterInit()（只有磁轴键盘有）
    }

    /// <summary>
    /// 映射设备类型 —— 即指令 [1] 字节。
    ///
    /// ★ 重要：固件里有两套互相冲突的编号。
    ///   HIDcmdHandler.h 用 MAP_TYPE_* 命名：KEYBOARD=1, MOUSE=2, GAMEPAD=3, CONSUMER=4
    ///   Universal.h    用 KEYBOARD=1, CONTROLLER=2, MOUSE=3, MULTIMEDIA=4
    ///   而 device 字节是从指令直接存进 EEPROM、并被 KeyManager.h 的
    ///   keyOperation() switch 按 Universal.h 的编号解释。
    ///   所以实际生效的是 Universal.h 的编号：2=手柄, 3=鼠标。
    ///   本上位机使用**实际生效**的编号，否则鼠标会变成手柄按键。
    /// </summary>
    public enum MapDevice : byte
    {
        Keyboard = 1,   // KEYBOARD     -> report.keyboardKeys[]
        Gamepad  = 2,   // CONTROLLER   -> report.xboxButtons
        Mouse    = 3,   // MOUSE        -> report.mouseButtons
        Consumer = 4,   // MULTIMEDIA   -> report.mediaButtons
    }

    public enum MapOperation : byte
    {
        Append   = 0x00,
        Override = 0x01,
        Clear    = 0x02,
    }

    /// <summary>磁轴按键参数编号 —— 即指令 [1] 字节。</summary>
    public enum KeyParameter : byte
    {
        RtActive      = 4,    // RT 触发（按下行程）
        RtDeactive    = 6,    // RT 释放（抬起行程）
        UpperDeadzone = 8,    // 上死区
        LowerDeadzone = 10,   // 下死区
    }

    public enum CalibrateMode : byte
    {
        All    = 0x00,   // CQ_ALL
        Single = 0x01,   // CQ_SINGLE
    }

    public enum QueryMode : byte
    {
        All    = 0x00,
        Single = 0x01,
    }

    public enum QueryTarget : byte
    {
        Mapping     = 0x00,
        Parameter   = 0x01,
        Calibration = 0x02,
        /// <summary>灯效参数（固件回 0x0C / 0x0D 两帧，最后 FIN）。</summary>
        Led         = 0x03,
        /// <summary>按键布局（固件逐键回 0xFE 帧，最后回 FIN）。</summary>
        Layout      = 0xFE,
        /// <summary>设备信息（固件 HIDDeviceInfoUpload）—— 回一帧就没有了，没有 FIN。</summary>
        DeviceInfo  = 0xFF,
    }

    /// <summary>
    /// 键盘型号配置（换板子主要改这里）。
    ///
    /// ★ 复用到别的键盘时：把 KeyCount 改成实际按键数即可 ——
    ///   按键按钮是按 KeyCount 动态生成的，映射列表按 MaxBindingsPerKey 走，
    ///   右侧再换一个对应的可视化选键控件就行。
    ///   例：6 键传统机械小键盘 → KeyCount = 6，选键控件换成 6 键布局。
    /// </summary>
    public static class DeviceLimits
    {
        /// <summary>BUTTONCOUNT：按键编号 0 ~ KeyCount-1（当前板子是 14，即 0~13）。</summary>
        public const int KeyCount = 14;

        /// <summary>MAXIMUM_BINDABLE_KEY：每个按键最多几条映射。</summary>
        public const int MaxBindingsPerKey = 12;

        /// <summary>RT 触发 / 释放行程（千分比）：1% ~ 50% 行程。</summary>
        public const int RtValueMin = 10;
        public const int RtValueMax = 500;

        /// <summary>上 / 下死区（千分比）：0 ~ 20% 行程，0 = 关闭死区。</summary>
        public const int DeadzoneValueMin = 0;
        public const int DeadzoneValueMax = 200;

        /// <summary>新参数的默认值（4% 行程）。</summary>
        public const int ParamValueDefault = 40;

        /// <summary>死区参数（固件对它们允许 0~200，RT 是 10~500）。</summary>
        public static bool IsDeadzone(KeyParameter p)
        {
            return p == KeyParameter.UpperDeadzone || p == KeyParameter.LowerDeadzone;
        }

        /// <summary>某个参数允许的最小值。</summary>
        public static int ParamMin(KeyParameter p)
        {
            return IsDeadzone(p) ? DeadzoneValueMin : RtValueMin;
        }

        /// <summary>某个参数允许的最大值。</summary>
        public static int ParamMax(KeyParameter p)
        {
            return IsDeadzone(p) ? DeadzoneValueMax : RtValueMax;
        }
    }

    /// <summary>构造 8 字节指令。</summary>
    public static class CommandBuilder
    {
        private static byte _sequence;

        /// <summary>下一个指令序号（同时保证相邻指令字节不完全相同）。</summary>
        public static byte NextSequence()
        {
            _sequence++;
            if (_sequence == 0) _sequence = 1;   // 0 保留，避免和固件里的全零 pre_cmd 撞上
            return _sequence;
        }

        private static byte[] New(HidCmd cmd)
        {
            var d = new byte[HidNative.CommandSize];
            d[0] = (byte)cmd;
            d[7] = NextSequence();
            return d;
        }

        public static byte[] SetMap(MapDevice device, byte key, byte offset,
                                   MapOperation operation, byte storageIndex)
        {
            var d = New(HidCmd.SetMap);
            d[1] = (byte)device;
            d[2] = key;
            d[3] = offset;
            d[4] = (byte)operation;
            d[5] = storageIndex;
            return d;
        }

        /// <summary>灯效参数保存时的确认字节（和固件里的 LED_PARAMS_MAGIC 一致）。</summary>
        public const byte LedSaveMagic = 0x5A;

        /// <summary>即时设置灯效（效果 / 亮度 / 速度 / 主色），不写 EEPROM。</summary>
        public static byte[] SetLed(byte effect, byte brightness, byte speed, byte r, byte g, byte b)
        {
            var d = New(HidCmd.SetLed);
            d[1] = effect;
            d[2] = brightness;
            d[3] = speed;
            d[4] = r;
            d[5] = g;
            d[6] = b;
            return d;
        }

        /// <summary>即时设置副色（跑马灯 / 双色呼吸用），不写 EEPROM。</summary>
        public static byte[] SetLedAccent(byte r, byte g, byte b)
        {
            var d = New(HidCmd.SetLedAccent);
            d[1] = r;
            d[2] = g;
            d[3] = b;
            return d;
        }

        /// <summary>单独设置灯效刷新率（10~200 fps），即时生效。</summary>
        public static byte[] SetLedFps(byte fps)
        {
            var d = New(HidCmd.SetLedFps);
            d[1] = fps;
            return d;
        }

        /// <summary>把当前灯效参数写进键盘 EEPROM。</summary>
        public static byte[] SaveLed()
        {
            var d = New(HidCmd.SaveLed);
            d[1] = LedSaveMagic;
            return d;
        }

        public static byte[] SetParameter(KeyParameter parameter, byte key, ushort value)
        {
            var d = New(HidCmd.SetParam);
            d[1] = (byte)parameter;
            d[2] = key;
            d[3] = (byte)(value >> 8);      // 高字节
            d[4] = (byte)(value & 0xFF);    // 低字节
            return d;
        }

        /// <summary>
        /// 刷新请求：让下位机把 EEPROM 重新读进内存。
        /// [1] = REFRESH_MAPPING / REFRESH_PARAM
        /// </summary>
        public static byte[] Refresh(RefreshTarget what)
        {
            var d = New(HidCmd.Refresh);
            d[1] = (byte)what;
            return d;
        }

        public static byte[] Calibrate(CalibrateMode mode, byte key)
        {
            var d = New(HidCmd.Calibrate);
            d[1] = (byte)mode;
            d[2] = key;
            return d;
        }

        /// <summary>
        /// 查询请求。
        ///
        /// 字节布局（与固件 HIDcmdHandler.h 的 CMD_QUERY 分支一致）：
        ///   [1] = 查询内容 QUERY_MAPPING / QUERY_PARAM   ← 放在这里，不占用 Byte5
        ///   [2] = CQ_ALL / CQ_SINGLE（全局 / 单键）
        ///   [3] = 按键号（单键查询时有效）
        ///   [4] = 0（空出来）
        /// </summary>
        public static byte[] Query(QueryTarget what, QueryMode scope, byte key)
        {
            var d = New(HidCmd.Query);
            d[1] = (byte)what;
            d[2] = (byte)scope;
            d[3] = key;
            return d;
        }

        public static string Hex(byte[] data)
        {
            if (data == null) return string.Empty;
            return string.Join(" ", data.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
        }
    }

    // ============================================================
    //  下位机回传数据的解析
    //
    //  固件的回传全部用 Report ID 4、8 字节，且"格式与上位机下发时一致"，
    //  所以这里可以按下发时同样的字段位置去读：
    //    应答   [0]=CMD_ACK(0x05)     [1]=被应答的指令 [2]=序号
    //    完成   [0]=CMD_FIN(0x06)     [1]=完成的指令   [2]=按键号（校准用） [7]=序号
    //    键值   [0]=CMD_SET_MAP       [1]=device [2]=key [3]=offset [4]=OVERRIDE [5]=slot
    //    参数   [0]=CMD_SET_PARAM     [1]=参数编号 [2]=key [3]=值高 [4]=值低
    // ============================================================

    public enum ResponseKind
    {
        Unknown,
        Ack,
        Fin,
        MapItem,
        ParamItem,
        DeviceInfo,
        LayoutItem,
        LedBasic,
        LedColors,
    }

    /// <summary>
    /// 设备信息（固件 HIDDeviceInfoUpload()），一条独立的 8 字节报文。
    ///
    /// 字节含义（照固件里的注释）：
    ///   [0]      0xFF          这条是设备信息报文（正好和 QUERY 的 [1]=0xFF 对应）
    ///   [1]      BUTTONCOUNT   按键数量
    ///   [2]      是否磁轴       0xF0 = 是，0x0F = 否
    ///   [3]      编码器数量     0 = 没有
    ///   [4]      设备类型       保留给后续手台主控使用
    ///   [5]..[7] 保留           可能会用于设备 Layout 识别
    ///
    /// 注意：这条回复之后固件**不会**再发 FIN，所以它自己就是这次查询的结束标志。
    /// 后面三项现在用不到，先原样存着，留给之后的功能。
    /// </summary>
    /// <summary>灯效基本参数帧（固件 LED_ITEM_BASIC = 0x0C）。</summary>
    public sealed class LedBasicFrame
    {
        public const byte FrameId = 0x0C;
        public byte Effect;
        public byte Brightness;
        public byte Speed;
        public byte Fps;

        public static LedBasicFrame FromFrame(byte[] r)
        {
            if (r == null || r.Length < 8 || r[0] != FrameId) return null;
            return new LedBasicFrame
            {
                Effect = r[1],
                Brightness = r[2],
                Speed = r[3],
                Fps = r[4],
            };
        }
    }

    /// <summary>灯效颜色帧（固件 LED_ITEM_COLORS = 0x0D）。</summary>
    public sealed class LedColorsFrame
    {
        public const byte FrameId = 0x0D;
        public byte R1, G1, B1, R2, G2, B2;

        public static LedColorsFrame FromFrame(byte[] r)
        {
            if (r == null || r.Length < 8 || r[0] != FrameId) return null;
            return new LedColorsFrame
            {
                R1 = r[1], G1 = r[2], B1 = r[3],
                R2 = r[4], G2 = r[5], B2 = r[6],
            };
        }
    }

    public sealed class DeviceInfo
    {
        public const byte FrameId = 0xFF;
        public const byte HallYes = 0xF0;
        public const byte HallNo  = 0x0F;

        /// <summary>按键数量（固件的 BUTTONCOUNT）。</summary>
        public int KeyCount;

        /// <summary>是否为磁轴键盘（[2] == 0xF0）。</summary>
        public bool HasHallSensor;

        /// <summary>[2] 的原始值，将来编码方式变了也不会丢信息。</summary>
        public byte HallByte;

        /// <summary>编码器数量（0 = 没有）。</summary>
        public int EncoderCount;

        /// <summary>设备类型（保留给后续手台主控）。</summary>
        public int DeviceType;

        /// <summary>[7]：固件版本号（2|3|3 位切分，见 VersionInfo.cs）。</summary>
        public FirmwareVersion Firmware;

        /// <summary>[6]：固件要求的上位机版本（2|3|3 位；0 = 旧固件，无要求）。</summary>
        public byte HostRequiredRaw;

        /// <summary>[6] 解码出来的版本号。</summary>
        public FirmwareVersion HostRequired;

        /// <summary>[5] 布局类型：0x00 旧设备（不上传布局）、0xFF 自定义布局。</summary>
        public byte LayoutKind;

        /// <summary>旧固件：没有布局信息，按固定网格显示。</summary>
        public const byte LayoutKindLegacy = 0x00;

        /// <summary>自定义布局：连接后按 QUERY_LAYOUT 读取按键位置。</summary>
        public const byte LayoutKindCustom = 0xFF;

        /// <summary>是否会上传布局。</summary>
        public bool HasCustomLayout { get { return LayoutKind == LayoutKindCustom; } }

        /// <summary>保留的三位，先存着备用。</summary>
        public byte[] Reserved = new byte[3];

        /// <summary>整帧原样留一份。</summary>
        public byte[] Raw;

        public static DeviceInfo FromFrame(byte[] r)
        {
            if (r == null || r.Length < 8 || r[0] != FrameId) return null;

            var info = new DeviceInfo();
            info.KeyCount      = r[1];
            info.HallByte      = r[2];
            info.HasHallSensor = (r[2] == HallYes);
            info.EncoderCount  = r[3];
            info.DeviceType    = r[4];
            info.LayoutKind    = r[5];
            info.Reserved[0]   = r[5];
            info.Reserved[1]   = r[6];
            info.Reserved[2]   = r[7];
            info.HostRequiredRaw = r[6];                           //倒数第二位：要求的上位机版本
            info.HostRequired    = FirmwareVersion.FromByte(r[6]);
            info.Firmware      = FirmwareVersion.FromByte(r[7]);   //最后一位是固件版本
            info.Raw           = (byte[])r.Clone();
            return info;
        }

        public override string ToString()
        {
            string hall = HasHallSensor ? "磁轴"
                        : (HallByte == HallNo ? "非磁轴" : string.Format("未知(0x{0:X2})", HallByte));

            string layout = HasCustomLayout ? L.T("自定义布局", "custom layout")
                          : (LayoutKind == LayoutKindLegacy ? L.T("旧设备", "legacy device")
                                                            : L.F("布局 0x{0:X2}", "layout 0x{0:X2}", LayoutKind));

            return L.F("{0} 键 · {1} · 编码器 {2} 个 · 设备类型 0x{3:X2} · {4}",
                       "{0} keys · {1} · {2} encoder(s) · device type 0x{3:X2} · {4}",
                       KeyCount, hall, EncoderCount, DeviceType, layout);
        }
    }

    /// <summary>
    /// 单键布局帧（固件 0xFE）：一条 8 字节 = 一个按键。
    ///
    ///   [0] 0xFE  [1] 按键索引  [2] x  [3] y  [4] w  [5] h  [6] flags  [7] 序号
    ///
    /// 坐标单位 0.25u（4 = 1u），左上角为原点，x 向右、y 向下。
    /// flags：bit7|bit6 = 11 表示该键需要旋转（旋转数据二期用 0xFD 帧）；
    ///        bit5..bit0 为按键类型（0 普通 / 1 修饰 / 2 编码器 / 3 装饰）。
    /// </summary>
    public sealed class KeyLayoutItem
    {
        public const byte FrameId = 0xFE;

        /// <summary>每 1u 对应的坐标单位数（0.25u 一个单位）。</summary>
        public const int UnitsPerU = 4;

        public int Index;
        public int X, Y, W, H;
        public byte Flags;

        public bool NeedsRotation { get { return (Flags & 0xC0) == 0xC0; } }
        public int KeyType { get { return Flags & 0x3F; } }

        public float UnitsX { get { return X / (float)UnitsPerU; } }
        public float UnitsY { get { return Y / (float)UnitsPerU; } }
        public float UnitsW { get { return W / (float)UnitsPerU; } }
        public float UnitsH { get { return H / (float)UnitsPerU; } }

        public static KeyLayoutItem FromFrame(byte[] r)
        {
            if (r == null || r.Length < 8 || r[0] != FrameId) return null;
            return new KeyLayoutItem
            {
                Index = r[1], X = r[2], Y = r[3], W = r[4], H = r[5], Flags = r[6],
            };
        }
    }

    public static class ResponseParser
    {
        public static ResponseKind KindOf(byte[] r)
        {
            if (r == null || r.Length < 8) return ResponseKind.Unknown;
            if (r[0] == DeviceInfo.FrameId) return ResponseKind.DeviceInfo;
            if (r[0] == KeyLayoutItem.FrameId) return ResponseKind.LayoutItem;
            if (r[0] == LedBasicFrame.FrameId) return ResponseKind.LedBasic;
            if (r[0] == LedColorsFrame.FrameId) return ResponseKind.LedColors;

            switch ((HidCmd)r[0])
            {
                case HidCmd.Ack: return ResponseKind.Ack;
                case HidCmd.Fin: return ResponseKind.Fin;
                case HidCmd.SetMap: return ResponseKind.MapItem;
                case HidCmd.SetParam: return ResponseKind.ParamItem;
                default: return ResponseKind.Unknown;
            }
        }

        /// <summary>被应答 / 被完成的指令码（Byte2）。</summary>
        public static byte ReferenceCmd(byte[] r) { return r[1]; }

        /// <summary>上位机写在 Byte8 的序号，用来把回传和指令对上。</summary>
        public static byte Sequence(byte[] r) { return r[7]; }

        /// <summary>
        /// 回传里带的"上位机序号"，用来确认这条回应确实是我们要的那条的。
        /// ACK 是我们自己拼的 {CMD_ACK, 指令码, 序号, 0...}，序号在 [2]；
        /// FIN 是把收到的指令原样回传只换 [0]，序号在 [7]（[2] 让给按键号了）。
        /// </summary>
        public static byte EchoSequence(byte[] r)
        {
            return KindOf(r) == ResponseKind.Ack ? r[2] : r[7];
        }

        /// <summary>FIN 里的按键号（校准进度用；其它指令是原样回传的 Byte3）。</summary>
        public static byte FinKey(byte[] r) { return r[2]; }

        public static byte MapDeviceByte(byte[] r) { return r[1]; }
        public static byte MapKey(byte[] r) { return r[2]; }
        public static byte MapOffset(byte[] r) { return r[3]; }
        public static byte MapSlot(byte[] r) { return r[5]; }

        public static int LayoutIndex(byte[] r) { return r[1]; }

        public static KeyParameter ParamCode(byte[] r) { return (KeyParameter)r[1]; }
        public static byte ParamKey(byte[] r) { return r[2]; }
        public static ushort ParamValue(byte[] r) { return (ushort)((r[3] << 8) | r[4]); }

        /// <summary>把一条键值回传变成可以直接加进本地列表的 Binding。</summary>
        public static Binding ToBinding(byte[] r)
        {
            return new Binding((MapDevice)MapDeviceByte(r), MapOffset(r));
        }
    }

    // ============================================================
    //  下拉框 / 画布用的候选项
    // ============================================================

    public sealed class OffsetOption
    {
        public byte Offset;
        public string Name;
        public string Note;

        public override string ToString()
        {
            return string.IsNullOrEmpty(Note)
                ? string.Format("{0} (0x{1:X2})", L.T(Name), Offset)
                : string.Format("{0} (0x{1:X2}) - {2}", L.T(Name), Offset, L.T(Note));
        }
    }

    public sealed class ParameterOption
    {
        public KeyParameter Code;
        public string Name;
        public string Note;

        public override string ToString() { return L.T(Name); }
    }

    /// <summary>设备类型 / 偏移量的可读名称表。</summary>
    public static class BindingCatalog
    {
        public static string DeviceName(MapDevice d)
        {
            switch (d)
            {
                case MapDevice.Keyboard: return L.T("键盘");
                case MapDevice.Gamepad:  return L.T("手柄");
                case MapDevice.Mouse:    return L.T("鼠标");
                case MapDevice.Consumer: return L.T("多媒体");
                default: return L.T("未知");
            }
        }

        // ---------------- 鼠标 ----------------
        // 固件: report.mouseButtons |= 1 << offset
        // 描述符声明 3 个按钮，因此只有 bit0/1/2 有效。
        private static readonly OffsetOption[] MouseOptions =
        {
            new OffsetOption { Offset = 0, Name = "左键",   Note = "mouseButtons bit0" },
            new OffsetOption { Offset = 1, Name = "右键",   Note = "mouseButtons bit1" },
            new OffsetOption { Offset = 2, Name = "中键/滚轮按下", Note = "mouseButtons bit2" },
        };

        // ---------------- 手柄 ----------------
        // 固件: report.xboxButtons |= 1 << offset（uint16，最多 16 个按钮）
        private static readonly OffsetOption[] GamepadOptions =
        {
            new OffsetOption { Offset = 0,  Name = "B1  (A)",     Note = "bit0" },
            new OffsetOption { Offset = 1,  Name = "B2  (B)",     Note = "bit1" },
            new OffsetOption { Offset = 2,  Name = "B3  (X)",     Note = "bit2" },
            new OffsetOption { Offset = 3,  Name = "B4  (Y)",     Note = "bit3" },
            new OffsetOption { Offset = 4,  Name = "B5  (LB)",    Note = "bit4" },
            new OffsetOption { Offset = 5,  Name = "B6  (RB)",    Note = "bit5" },
            new OffsetOption { Offset = 6,  Name = "B7  (Back)",  Note = "bit6" },
            new OffsetOption { Offset = 7,  Name = "B8  (Start)", Note = "bit7" },
            new OffsetOption { Offset = 8,  Name = "B9  (LS)",    Note = "bit8" },
            new OffsetOption { Offset = 9,  Name = "B10 (RS)",    Note = "bit9" },
            new OffsetOption { Offset = 10, Name = "B11 (Up)",    Note = "bit10" },
            new OffsetOption { Offset = 11, Name = "B12 (Down)",  Note = "bit11" },
            new OffsetOption { Offset = 12, Name = "B13 (Left)",  Note = "bit12" },
            new OffsetOption { Offset = 13, Name = "B14 (Right)", Note = "bit13" },
            new OffsetOption { Offset = 14, Name = "B15 (Guide)", Note = "bit14" },
            new OffsetOption { Offset = 15, Name = "B16 (保留)",  Note = "bit15" },
        };

        // ---------------- 多媒体 / 消费类 ----------------
        // 固件: report.mediaButtons |= 1 << offset
        // 描述符（SubHID.cpp, Report ID 5）按顺序声明 6 个用法 → bit 0..5
        private static readonly OffsetOption[] ConsumerOptions =
        {
            new OffsetOption { Offset = 0, Name = "静音",       Note = "Mute, bit0" },
            new OffsetOption { Offset = 1, Name = "音量 -",     Note = "Volume Down, bit1" },
            new OffsetOption { Offset = 2, Name = "音量 +",     Note = "Volume Up, bit2" },
            new OffsetOption { Offset = 3, Name = "播放/暂停",  Note = "Play/Pause, bit3" },
            new OffsetOption { Offset = 4, Name = "下一曲",     Note = "Next Track, bit4" },
            new OffsetOption { Offset = 5, Name = "上一曲",     Note = "Previous Track, bit5" },
        };

        /// <summary>获取某设备类型下所有可选的偏移量。</summary>
        public static List<OffsetOption> Options(MapDevice device)
        {
            switch (device)
            {
                case MapDevice.Keyboard: return KeyboardOptions();
                case MapDevice.Mouse:    return MouseOptions.ToList();
                case MapDevice.Gamepad:  return GamepadOptions.ToList();
                case MapDevice.Consumer: return ConsumerOptions.ToList();
                default: return new List<OffsetOption>();
            }
        }

        // ── 键盘修饰键：必须换算过的偏移量 ────────────────────────────────────
        //
        // 固件 keyOperation() 的 KEYBOARD 分支是：
        //     report.keyboardKeys[高位] |= 1 << 低位
        // 其中「高位/低位」来自 offsetToArrayIndex(offset) = ((offset/8) << 4) | (offset%8)。
        //
        // 而 SubHID.cpp 实际发出的键盘报文是 14 字节：
        //     keyboardKeys[0..12] = 13 字节普通键（HID usage 0..103，一位一个键）
        //     keyboardKeys[13]    = 1 字节修饰键（usage 0xE0..0xE7 → bit0..bit7）
        //
        // 所以修饰键要落在 [13] 上，必须让「高位 = 13、低位 = usage-0xE0」，
        // 也就是下发 offset = 13*8 + (usage - 224) = 104 + (usage - 224) = 104..111。
        //
        // （直接把 usage 224..231 发下去，会被 (high<<4)|low 截断成 0xC0..0xC7，
        //   高位变成 12 → 落到普通键区第 12 字节，写错位置。）
        public const byte KeyboardModifierOffsetBase = 104;   // = 13 * 8

        /// <summary>选键控件给出的偏移量 → 指令里实际要下发的 offset。</summary>
        public static byte ToWireOffset(MapDevice device, byte pickerOffset)
        {
            if (device == MapDevice.Keyboard && pickerOffset >= 224 && pickerOffset <= 231)
                return (byte)(KeyboardModifierOffsetBase + (pickerOffset - 224));
            return pickerOffset;
        }

        /// <summary>指令里的 offset → 选键控件 / 显示用的偏移量（ToWireOffset 的逆运算）。</summary>
        public static byte ToPickerOffset(MapDevice device, byte wireOffset)
        {
            if (device == MapDevice.Keyboard
                && wireOffset >= KeyboardModifierOffsetBase
                && wireOffset <= KeyboardModifierOffsetBase + 7)
                return (byte)(224 + (wireOffset - KeyboardModifierOffsetBase));
            return wireOffset;
        }

        /// <summary>
        /// 键盘可选项。
        /// Offset 字段是「选键控件用的偏移量」= HID Usage ID（修饰键 224~231），
        /// 真正下发前用 ToWireOffset() 换算成 104~111。
        /// </summary>
        public static List<OffsetOption> KeyboardOptions()
        {
            var list = new List<OffsetOption>();
            foreach (var pair in Keyboard.KeyNameByHidId.OrderBy(p => p.Key))
            {
                int usage = pair.Key;
                bool isModifier = usage >= 224 && usage <= 231;

                // 普通键：usage 4..103 → keyboardKeys[0..12]
                // 修饰键：usage 224..231 → keyboardKeys[13]
                // 104..127 会和修饰键抢同一个字节，本板布局里没有这些键，不提供。
                if (!isModifier && (usage < 4 || usage > 103)) continue;

                list.Add(new OffsetOption
                {
                    Offset = (byte)usage,
                    Name = pair.Value,
                    Note = isModifier
                        ? string.Format("修饰键 HID 0x{0:X2} → keyboardKeys[13] bit{1}（下发 0x{2:X2}）",
                                        usage, usage - 224, ToWireOffset(MapDevice.Keyboard, (byte)usage))
                        : string.Format("HID 0x{0:X2} → keyboardKeys[{1}] bit{2}", usage, usage / 8, usage % 8),
                });
            }
            return list;
        }

        /// <summary>把 (设备, 下发用的 offset) 翻译成可读文本。</summary>
        public static string Describe(MapDevice device, byte wireOffset)
        {
            byte picker = ToPickerOffset(device, wireOffset);
            var hit = Options(device).FirstOrDefault(o => o.Offset == picker);
            if (hit != null) return string.Format("{0} {1}", DeviceName(device), hit.Name);
            return string.Format("{0} offset 0x{1:X2}", DeviceName(device), wireOffset);
        }

        /// <summary>
        /// 键帽上用的短文本：键盘只留按键名（A / F1 / 空格），
        /// 其它设备保留“设备 名称”（鼠标 左键），太长会由绘制方省略。
        /// </summary>
        public static string ShortText(Binding b)
        {
            if (b == null) return string.Empty;
            string full = Describe(b.Device, b.Offset);
            string dev = DeviceName(b.Device);
            if (b.Device == MapDevice.Keyboard && full.StartsWith(dev + " ", StringComparison.Ordinal))
                return full.Substring(dev.Length + 1);
            return full;
        }

        /// <summary>
        /// 键帽专用的最短文本：只留按键名（A、F1、左键、右键），
        /// 用于「A+B」这种两条映射并排显示，避免被设备名挤掉。
        /// </summary>
        public static string CapText(Binding b)
        {
            if (b == null) return string.Empty;
            byte picker = ToPickerOffset(b.Device, b.Offset);
            var hit = Options(b.Device).FirstOrDefault(o => o.Offset == picker);
            return hit != null ? hit.Name : Describe(b.Device, b.Offset);
        }

        /// <summary>描述一个映射，用于日志和列表显示。</summary>
        public static string Describe(Binding b)
        {
            if (b == null) return "(空)";
            return string.Format("{0}  [device={1}, offset=0x{2:X2}]",
                Describe(b.Device, b.Offset), (byte)b.Device, b.Offset);
        }

        // ---------------- 参数 ----------------
        public static readonly ParameterOption[] Parameters =
        {
            new ParameterOption { Code = KeyParameter.RtActive,      Name = "RT 触发行程",  Note = "按下时触发所需的行程" },
            new ParameterOption { Code = KeyParameter.RtDeactive,    Name = "RT 释放行程",  Note = "抬起时释放所需的行程" },
            new ParameterOption { Code = KeyParameter.UpperDeadzone, Name = "上死区",       Note = "接近底部时的死区" },
            new ParameterOption { Code = KeyParameter.LowerDeadzone, Name = "下死区",       Note = "接近顶部时的死区" },
        };

        public static ParameterOption Parameter(KeyParameter code)
        {
            return Parameters.FirstOrDefault(p => p.Code == code) ?? Parameters[0];
        }

        /// <summary>
        /// 参数值 0~1000 表示满行程的千分比（固件里 temp/1000 * (max-min)）。
        /// </summary>
        public static string FormatParameterValue(int value)
        {
            return L.F("= {0} / 1000（满行程的 {1:0.0}%）", "= {0} / 1000 ({1:0.0}% of travel)",
                value, value / 10.0);
        }
    }

    /// <summary>一条按键映射（设备 + 偏移量）。</summary>
    public sealed class Binding
    {
        public MapDevice Device;
        public byte Offset;

        public Binding() { }

        public Binding(MapDevice device, byte offset)
        {
            Device = device;
            Offset = offset;
        }

        public string Text { get { return BindingCatalog.Describe(this); } }

        public Binding Clone() { return new Binding(Device, Offset); }

        public override string ToString() { return Text; }
    }
}
