using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace RedisClient
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {


            //注册异常事件
            AppDomain.CurrentDomain.UnhandledException += (s, arg) =>
            {
                var ex = (Exception)arg.ExceptionObject;
                if (arg.IsTerminating) //如果崩溃直接关闭
                {

                    MessageBox.Show("很抱歉，软件出现异常，请重启软件！", "", MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(Environment.ExitCode);
                }
            };
            this.DispatcherUnhandledException += (s, arg) =>
            {
                if (arg.Exception != null)
                {
                    arg.Exception.Log("DispatcherUnhandledException");
                    MessageBox.Show("很抱歉，软件出现异常" + arg.Exception.Message, "", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                arg.Handled = true;
            };

            base.OnStartup(e);
        }
    }
}
