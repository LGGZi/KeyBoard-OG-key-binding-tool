using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace KeyboardOG
{
    /// <summary>
    /// 上位机的可调参数（发送速度、等待超时、是否等 FIN）。
    ///
    /// 保存在 exe 同级目录（"根目录"）的 KeyboardOG.settings.ini 里，下次启动自动读回来；
    /// 文件不存在时用默认值，并**自动生成**这个文件。
    ///
    /// 默认值就是现在能正常工作的那一套：下位机是 500Hz 的上报节奏，
    /// 所以发送间隔默认也按 500Hz（= 2ms/条）来。
    /// </summary>
    public sealed class AppSettings
    {
        public const string FileName = "KeyboardOG.settings.ini";

        public const int DefaultSendRateHz = 500;
        public const int DefaultResponseTimeoutMs = 500;
        public const bool DefaultWaitForFin = true;

        /// <summary>下限 10Hz（100ms 一条），上限 2000Hz（间隔按 1ms 兜底）。</summary>
        public const int MinSendRateHz = 10;
        public const int MaxSendRateHz = 2000;
        public const int MinTimeoutMs = 20;
        public const int MaxTimeoutMs = 5000;

        /// <summary>发送频率（Hz）。500 = 每条之间至少 2ms。</summary>
        public int SendRateHz = DefaultSendRateHz;

        /// <summary>等待下位机回应的超时（ms）。</summary>
        public int ResponseTimeoutMs = DefaultResponseTimeoutMs;

        /// <summary>是否等键盘回 FIN 之后再发下一条。关掉会丢指令，一般不要关。</summary>
        public bool WaitForFin = DefaultWaitForFin;

        /// <summary>批量发送时两条指令之间的最小间隔（ms），由发送频率换算。</summary>
        public int MinGapMs
        {
            get { return Math.Max(1, (int)Math.Round(1000.0 / Math.Max(1, SendRateHz))); }
        }

        public static string FilePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName); }
        }

        public AppSettings Clone()
        {
            return new AppSettings
            {
                SendRateHz = SendRateHz,
                ResponseTimeoutMs = ResponseTimeoutMs,
                WaitForFin = WaitForFin,
            };
        }

        public void ResetToDefault()
        {
            SendRateHz = DefaultSendRateHz;
            ResponseTimeoutMs = DefaultResponseTimeoutMs;
            WaitForFin = DefaultWaitForFin;
        }

        public void Clamp()
        {
            if (SendRateHz < MinSendRateHz) SendRateHz = MinSendRateHz;
            if (SendRateHz > MaxSendRateHz) SendRateHz = MaxSendRateHz;
            if (ResponseTimeoutMs < MinTimeoutMs) ResponseTimeoutMs = MinTimeoutMs;
            if (ResponseTimeoutMs > MaxTimeoutMs) ResponseTimeoutMs = MaxTimeoutMs;
        }

        public override string ToString()
        {
            return string.Format("{0} Hz（约 {1} ms/条）· 超时 {2} ms · {3}",
                SendRateHz, MinGapMs, ResponseTimeoutMs,
                WaitForFin ? "等 FIN 再发下一条" : "不等 FIN");
        }

        // ------------------------------------------------------------
        // 读写文件
        // ------------------------------------------------------------

        /// <summary>文件不存在 / 读失败时返回默认值，并把文件生成出来。</summary>
        public static AppSettings Load()
        {
            var s = new AppSettings();

            try
            {
                if (File.Exists(FilePath))
                {
                    foreach (var raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                    {
                        string line = raw.Trim();
                        if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;

                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;

                        string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                        string val = line.Substring(eq + 1).Trim();

                        int n;
                        switch (key)
                        {
                            case "sendratehz":
                                if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
                                    s.SendRateHz = n;
                                break;
                            case "responsetimeoutms":
                                if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
                                    s.ResponseTimeoutMs = n;
                                break;
                            case "waitforfin":
                                s.WaitForFin = (val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase));
                                break;
                        }
                    }
                    s.Clamp();
                    return s;
                }
            }
            catch
            {
                //文件坏了就当没有，按默认值跑
                s.ResetToDefault();
            }

            //第一次运行：用默认值生成文件
            try { s.Save(); } catch { }
            return s;
        }

        public void Save()
        {
            Clamp();

            var sb = new StringBuilder();
            sb.AppendLine("# KeyboardOG 设置文件");
            sb.AppendLine("# 删掉这个文件就会恢复默认值（下次启动自动重建）");
            sb.AppendLine("# SendRateHz：发送频率，单位 Hz。500 = 每条指令之间至少 2ms");
            sb.AppendLine("# ResponseTimeoutMs：等待键盘回应的超时，单位 ms");
            sb.AppendLine("# WaitForFin：1 = 每条指令等键盘回 FIN 之后再发下一条（推荐），0 = 不等");
            sb.AppendLine("SendRateHz=" + SendRateHz.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("ResponseTimeoutMs=" + ResponseTimeoutMs.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("WaitForFin=" + (WaitForFin ? "1" : "0"));

            //带 BOM：用记事本 / PowerShell 直接打开也不会乱码（自己读的时候 Encoding.UTF8 会跳过 BOM）
            File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(true));
        }
    }
}
