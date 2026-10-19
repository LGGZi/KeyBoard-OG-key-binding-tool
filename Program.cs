using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace KeyboardOG
{
    internal static class Program
    {
        private static readonly string CrashLogPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "KeyboardOG.crash.log");

        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 上位机会直接操作键盘的 EEPROM，出问题时不希望界面无声消失，
            // 所以把未处理异常写到 exe 同目录的 KeyboardOG.crash.log 里。
            Application.ThreadException += (s, e) => Report(e.Exception, "UI 线程异常", true);
            AppDomain.CurrentDomain.UnhandledException +=
                (s, e) => Report(e.ExceptionObject as Exception, "未处理异常（进程即将退出）", false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            try
            {
                Application.Run(new Form1());
            }
            catch (Exception ex)
            {
                Report(ex, "启动失败", true);
                throw;
            }
        }

        /// <param name="showDialog">
        /// UI 线程异常是可恢复的，弹框告诉用户；
        /// AppDomain 级的未处理异常意味着进程马上就要退出，弹框只会卡住退出流程，所以只记日志。
        /// </param>
        private static void Report(Exception ex, string title, bool showDialog)
        {
            if (ex == null) return;

            var sb = new StringBuilder();
            sb.AppendLine("时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("类型: " + title);
            sb.AppendLine(ex.ToString());
            sb.AppendLine(new string('-', 70));

            try { File.AppendAllText(CrashLogPath, sb.ToString(), Encoding.UTF8); } catch { }

            if (!showDialog) return;

            try
            {
                MessageBox.Show(
                    title + "：\r\n\r\n" + ex.Message +
                    "\r\n\r\n详细信息已写入：\r\n" + CrashLogPath,
                    "KeyboardOG", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
    }
}
