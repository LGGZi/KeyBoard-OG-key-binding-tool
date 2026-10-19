using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace KLEconvert
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // 无界面：--export-h / --export-json        原样导出
            //         --number-h / --number-json       先按位置自动编号再导出
            if (args.Length >= 3 && (args[0] == "--export-h" || args[0] == "--export-json"
                                     || args[0] == "--number-h" || args[0] == "--number-json"))
            {
                try
                {
                    var layout = KleLayout.Parse(File.ReadAllText(args[1], Encoding.UTF8));
                    layout.SourceName = Path.GetFileName(args[1]);
                    if (args[0] == "--number-h" || args[0] == "--number-json") layout.AutoNumber();
                    string text = (args[0] == "--export-h" || args[0] == "--number-h")
                        ? layout.ToHeader("layout_custom", true)
                        : layout.ToJson(true);
                    File.WriteAllText(args[2], text, new UTF8Encoding(false));
                    Console.WriteLine("OK " + args[2]);
                    Environment.ExitCode = 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("FAIL " + ex.Message);
                    Environment.ExitCode = 1;
                }
                return;
            }

            // 自绘截图：--shot <out.png> [in.json] [--numbered]
            if (args.Length >= 2 && args[0] == "--shot")
            {
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    var f = new Form1();
                    if (args.Length >= 3 && File.Exists(args[2])) f.LoadPath(args[2]);
                    if (args.Length >= 4 && args[3] == "--numbered") f.ApplyAutoNumberForShot();
                    f.Show();
                    Application.DoEvents();
                    f.PerformLayout();
                    using (var bmp = new System.Drawing.Bitmap(f.Width, f.Height))
                    {
                        f.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, f.Width, f.Height));
                        bmp.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
                    }
                    f.Close();
                    Console.WriteLine("OK " + args[1]);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("FAIL " + ex);
                    Environment.ExitCode = 1;
                }
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var form = new Form1();
            if (args.Length >= 1 && File.Exists(args[0])) form.LoadPath(args[0]);
            Application.Run(form);
        }
    }
}
