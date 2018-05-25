using SAEA.Commom;
using SAEA.RPC.Provider;
//using SAEA.RPCTest.Consumer;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.RPCTest
{
    class Program
    {
        static void Main(string[] args)
        {
            ConsoleHelper.WriteLine($"SAEA.RPC功能测试： {Environment.NewLine}   p 启动rpc provider{Environment.NewLine}   c 启动rpc consumer{Environment.NewLine}   g 启动rpc consumer代码生成器");

            var inputStr = ConsoleHelper.ReadLine();

            if (string.IsNullOrEmpty(inputStr))
            {
                inputStr = "p";
            }

            if (inputStr == "c")
            {
                ConsoleHelper.WriteLine("开始Consumer测试！");
                ConsumerInit();
                ConsoleHelper.WriteLine("回车结束！");
                ConsoleHelper.ReadLine();
            }
            else if (inputStr == "a")
            {
                ProviderInit();
                ConsoleHelper.WriteLine("回车开始Consumer测试！");
                ConsoleHelper.ReadLine();
                ConsumerInit();
                ConsoleHelper.WriteLine("回车结束！");
                ConsoleHelper.ReadLine();
            }
            else if (inputStr == "g")
            {
                ConsoleHelper.WriteLine("正在代码生成中...");
                Generate();
                ConsoleHelper.WriteLine("代码生成完毕，回车结束！");
                ConsoleHelper.ReadLine();
            }
            else
            {
                ProviderInit();
                ConsoleHelper.WriteLine("回车结束！");
                ConsoleHelper.ReadLine();
            }
        }


        static void ProviderInit()
        {
            ConsoleHelper.Title = "SAEA.RPC.Provider";
            ConsoleHelper.WriteLine("Provider正在启动HelloService。。。");
            var sp = new ServiceProvider(new Type[] { typeof(Provider.HelloService) });
            sp.Start();
            ConsoleHelper.WriteLine("Provider就绪！");
        }

        static void Generate()
        {
            RPC.Generater.CodeGnerater.Generate(PathHelper.Current, "SAEA.RPCTest");
        }

        static void ConsumerInit()
        {
            //ConsoleHelper.Title = "SAEA.RPC.Consumer";

            //var url = "rpc://172.31.32.85:39654";

            //ConsoleHelper.WriteLine($"Consumer正在连接到{url}...");

            //RPCServiceProxy cp = new RPCServiceProxy(url);

            //ConsoleHelper.WriteLine("Consumer连接成功");

            //ConsoleHelper.WriteLine("HelloService/Hello:" + cp.HelloService.Hello());
            //ConsoleHelper.WriteLine("HelloService/Plus:" + cp.HelloService.Plus(1, 9));
            //ConsoleHelper.WriteLine("HelloService/Update/UserName:" + cp.HelloService.Update(new Consumer.Model.UserInfo() { ID = 1, UserName = "yswenli" }).UserName);
            //ConsoleHelper.WriteLine("HelloService/GetGroupInfo/Creator.UserName:" + cp.HelloService.GetGroupInfo(1).Creator.UserName);
            //ConsoleHelper.WriteLine("HelloService/SendData:" + System.Text.Encoding.UTF8.GetString(cp.HelloService.SendData(System.Text.Encoding.UTF8.GetBytes("Hello Data"))));
            //ConsoleHelper.WriteLine("回车启动性能测试！");

            //ConsoleHelper.ReadLine();

            #region 性能测试

            //Stopwatch sw = new Stopwatch();

            //int count = 1000000;

            //ConsoleHelper.WriteLine($"{count} 次实体传输调用测试中...");

            //var ui = new Consumer.Model.UserInfo() { ID = 1, UserName = "yswenli" };

            //sw.Start();

            //for (int i = 0; i < count; i++)
            //{
            //    cp.HelloService.Update(ui);
            //}
            //ConsoleHelper.WriteLine($"实体传输：{count * 1000 / sw.ElapsedMilliseconds} 次/秒");

            //sw.Stop();

            #endregion



        }
    }
}
