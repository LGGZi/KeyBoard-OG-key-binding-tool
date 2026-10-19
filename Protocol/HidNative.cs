using System;
using System.Runtime.InteropServices;
using System.Text;

namespace KeyboardOG
{
    /// <summary>
    /// 对 WindowsGUIIncludes 中 HIDComm.h / HIDComm.cpp 的 P/Invoke 声明。
    ///
    /// 该 C 库内部使用全局状态（选中的设备句柄、设备列表、错误缓冲），
    /// 因此**所有**导出函数必须在同一个线程上串行调用，见 <see cref="HidTransport"/>。
    /// </summary>
    public static class HidNative
    {
        public const int MaxDevices     = 64;
        public const int DevicePathMax  = 512;
        public const int StringMax      = 128;
        public const int CommandSize    = 8;
        public const byte CommandReportId = 0x04;

        public const string DllName = "HIDComm.dll";

        /// <summary>对应 C 结构体 HidDeviceInfo（顺序必须完全一致）。</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct HidDeviceInfo
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = DevicePathMax)]
            public byte[] DevicePath;

            public ushort VendorId;
            public ushort ProductId;
            public ushort UsagePage;
            public ushort Usage;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = StringMax)]
            public byte[] ProductName;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = StringMax)]
            public byte[] Manufacturer;

            public int IsCommandInterface;

            private static string Ansi(byte[] raw)
            {
                if (raw == null) return string.Empty;
                int len = Array.IndexOf(raw, (byte)0);
                if (len < 0) len = raw.Length;
                // 原生库用 CP_ACP 把宽字符转成窄字符，这里用同样的代码页还原。
                return Encoding.Default.GetString(raw, 0, len);
            }

            public string Path { get { return Ansi(DevicePath); } }
            public string Name { get { return Ansi(ProductName); } }
            public string VendorName { get { return Ansi(Manufacturer); } }

            /// <summary>用于界面下拉框显示。</summary>
            public string DisplayText
            {
                get
                {
                    string name = Name;
                    if (string.IsNullOrWhiteSpace(name)) name = "(无产品名)";

                    //列表里已经只剩指令接口了，这个标记没意义（而且会跟着语言变），去掉
            string mark = string.Empty;
                    string vendor = VendorName;
                    if (string.IsNullOrWhiteSpace(vendor)) vendor = "HID";

                    return string.Format("[{0}] VID_{1:X4}&PID_{2:X4} #{3}  {4}{5}",
                        vendor, VendorId, ProductId, InstanceHint, name, mark);
                }
            }

            /// <summary>
            /// 设备实例号（设备路径里 vid/pid 后面那一段）。
            ///
            /// 同一块板子会暴露多个集合（键盘 / 鼠标 / 手柄 / 多媒体 / 厂商指令），
            /// 它们的产品名和 VID/PID 完全一样，只有实例号和结尾的 colNN 不同；
            /// 两块一模一样的开发板（比如键盘 + 调试板）同理。
            /// 把这一段显示出来，才能在“发现 N 个指令接口”时选对目标。
            /// </summary>
            public string InstanceHint
            {
                get
                {
                    string p = Path;
                    if (string.IsNullOrEmpty(p)) return "?";

                    // 典型路径：\\?\hid#vid_2341&pid_8036&mi_00#8&2f0d3c1&0&0000#{guid}
                    // 最后那个 '#' 是实例号与 GUID 之间的分隔符，真正的实例号在它前面那一段。
                    int brace = p.LastIndexOf('{');
                    int end = brace > 0 ? brace : p.Length;
                    if (end > 0 && p[end - 1] == '#') end--;

                    int hash = end > 0 ? p.LastIndexOf('#', end - 1) : -1;
                    string seg = (hash >= 0 && hash + 1 < end)
                        ? p.Substring(hash + 1, end - hash - 1)
                        : p.Substring(0, end);

                    if (seg.Length > 22) seg = seg.Substring(seg.Length - 22);
                    return string.IsNullOrEmpty(seg) ? "?" : seg;
                }
            }
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Hid_EnumerateDevices([In, Out] HidDeviceInfo[] outDevices, int maxDevices);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Hid_SelectDevice(int index);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Hid_SelectCommandInterface(ushort vendorId, ushort productId);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Hid_SendCommand([In] byte[] data);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Hid_ReadResponse([Out] byte[] outData, int timeoutMs);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Hid_CloseDevice();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr Hid_GetLastError();

        public static string LastError
        {
            get
            {
                IntPtr p = Hid_GetLastError();
                return p == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(p);
            }
        }
    }
}
