using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace LazyTurtle
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Developer switch: "LazyTurtle.exe --floor 3" jumps straight onto floor 3.
            int startLevel = -1;
            bool debug = Array.IndexOf(args, "--debug") >= 0;
            bool software = Array.IndexOf(args, "--software") >= 0;   // force the CPU renderer
            for (int i = 0; i + 1 < args.Length; i++)
            {
                int n;
                if ((args[i] == "--floor" || args[i] == "--level") && int.TryParse(args[i + 1], out n)) startLevel = n - 1;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => Crash(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Crash(e.ExceptionObject as Exception);

            using (var form = new GameForm(startLevel, debug, software))
                Application.Run(form);
        }

        static void Crash(Exception ex)
        {
            string log = Path.Combine(SaveData.Folder, "error.log");
            try
            {
                Directory.CreateDirectory(SaveData.Folder);
                File.AppendAllText(log, DateTime.Now + Environment.NewLine + ex + Environment.NewLine + Environment.NewLine);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            MessageBox.Show("Ups! Coś poszło nie tak / Something went wrong.\n\n" + (ex == null ? "" : ex.Message) + "\n\n" + log,
                "Lazy Turtle", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(1);
        }
    }
}
