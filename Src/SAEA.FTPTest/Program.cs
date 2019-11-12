using System;
using System.Windows.Forms;

namespace SAEA.FTPTest
{
    class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                Application.ThreadException += Application_ThreadException;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{ex.Message}");
            }

        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"{(e.ExceptionObject as Exception).Message}");
        }

        private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            MessageBox.Show($"{e.Exception.Message}");
        }
    }

}
