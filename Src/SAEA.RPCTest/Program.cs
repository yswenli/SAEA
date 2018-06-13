using SAEA.Commom;
using SAEA.RPC.Provider;
using SAEA.RPCTest.Consumer;
using SAEA.RPCTest.Consumer.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.RPCTest
{
    class Program
    {
        static void Main(string[] args)
        {
            ConsoleHelper.WriteLine($"SAEA.RPC功能测试： {Environment.NewLine}   a 启动rpc provider consumer{Environment.NewLine}   p 启动rpc provider{Environment.NewLine}   c 启动rpc consumer{Environment.NewLine}   t 启动rpc 稳定性测试{Environment.NewLine}   g 启动rpc consumer代码生成器");

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
            else if (inputStr == "s")
            {
                ConsoleHelper.WriteLine("正在启动序列化测试...");
                SerializeTest();
                ConsoleHelper.WriteLine("代码生成完毕，回车结束！");
                ConsoleHelper.ReadLine();
            }
            else if (inputStr == "t")
            {
                StabilityTest();
                ConsoleHelper.WriteLine("回车结束！");
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

            //var sp = new ServiceProvider(new Type[] { typeof(Provider.HelloService) });
            var sp = new ServiceProvider();

            sp.Start();

            ConsoleHelper.WriteLine("Provider就绪！");
        }

        static void Generate()
        {
            RPC.Generater.CodeGnerater.Generate(PathHelper.Current, "SAEA.RPCTest");
        }

        static void ConsumerInit()
        {
            ConsoleHelper.Title = "SAEA.RPC.Consumer";

            var url = "rpc://127.0.0.1:39654";

            ConsoleHelper.WriteLine($"Consumer正在连接到{url}...");

            RPCServiceProxy cp = new RPCServiceProxy(url);

            ConsoleHelper.WriteLine("Consumer连接成功");

            Console.WriteLine("HelloService/Hello:" + cp.HelloService.Hello());
            Console.WriteLine("HelloService/Plus:" + cp.HelloService.Plus(1, 9));
            Console.WriteLine("HelloService/Update/UserName:" + cp.HelloService.Update(new Consumer.Model.UserInfo() { ID = 1, UserName = "yswenli" }).UserName);
            Console.WriteLine("HelloService/SendData:" + System.Text.Encoding.UTF8.GetString(cp.HelloService.SendData(System.Text.Encoding.UTF8.GetBytes("Hello Data"))));
            Console.WriteLine("");

            Console.WriteLine("GroupService/Add/ Creator.UserName:" + cp.GroupService.Add("rpc group", new Consumer.Model.UserInfo() { ID = 1, UserName = "yswenli" }).Creator.UserName);
            Console.WriteLine("GroupService/Update/Count:" + cp.GroupService.Update(new System.Collections.Generic.List<Consumer.Model.UserInfo>() { new Consumer.Model.UserInfo() { ID = 1, UserName = "yswenli" } }).Count);
            Console.WriteLine("GroupService/GetGroupInfo/Users.UserName:" + cp.GroupService.GetGroupInfo(1).Users[0].UserName);
            Console.WriteLine("");

            var dic = new Dictionary<int, Consumer.Model.UserInfo>();
            dic.Add(1, new Consumer.Model.UserInfo() { UserName = "yswenli" });
            Console.WriteLine("DicService/Test/UserName:" + cp.DicService.Test(1, dic)[1].UserName);
            Console.WriteLine("");


            ActionResult<UserInfo> data = new ActionResult<UserInfo>()
            {
                Code = 200,
                Error = string.Empty,
                Success = true,
                Data = new UserInfo()
                {
                    ID = 1,
                    UserName = "yswenli",
                    Birthday = DateTime.Now
                }
            };
            Console.WriteLine("GenericService/Get/UserName:" + cp.GenericService.Get(data).Data.UserName);

            ConsoleHelper.WriteLine("回车启动性能测试！");

            ConsoleHelper.ReadLine();

            #region 性能测试

            Stopwatch sw = new Stopwatch();

            int count = 1000000;

            ConsoleHelper.WriteLine($"{count} 次实体传输调用测试中...");

            var ui = new Consumer.Model.UserInfo() { ID = 1, UserName = "yswenli" };

            sw.Start();

            for (int i = 0; i < count; i++)
            {
                cp.HelloService.Update(ui);
            }
            ConsoleHelper.WriteLine($"实体传输：{count * 1000 / sw.ElapsedMilliseconds} 次/秒");

            sw.Stop();

            #endregion

        }


        static void SerializeTest()
        {
            var groupInfo = new GroupInfo()
            {
                GroupID = 1,
                IsTemporary = false,
                Name = "yswenli group",
                Created = DateTimeHelper.Now,
                Creator = new UserInfo()
                {

                    ID = 1,
                    Birthday = DateTimeHelper.Now.AddYears(-100),
                    UserName = "yswenli"
                },
                Users = new System.Collections.Generic.List<UserInfo>()
                {
                    new UserInfo()
                    {

                        ID = 1,
                        Birthday = DateTimeHelper.Now.AddYears(-100),
                        UserName = "yswenli"
                    }
                }
            };

            var count = 100000;

            Stopwatch sw = new Stopwatch();
            sw.Start();

            for (int i = 0; i < count; i++)
            {
                var bytes = RPC.Serialize.ParamsSerializeUtil.Serialize(groupInfo);

                int os = 0;

                var obj = RPC.Serialize.ParamsSerializeUtil.Deserialize(groupInfo.GetType(), bytes, ref os);
            }

            ConsoleHelper.WriteLine($"实体传输：{count * 1000 / sw.ElapsedMilliseconds} 次/秒");

            sw.Stop();

        }

        static void StabilityTest()
        {
            ProviderInit();

            Console.Title = "SAEA.RPC稳定性测试";

            var url = "rpc://127.0.0.1:39654";

            ConsoleHelper.WriteLine($"Consumer正在连接到{url}...");

            RPCServiceProxy cp = new RPCServiceProxy(new Uri(url), 1, 1);
            cp.OnErr += Cp_OnErr;

            ConsoleHelper.WriteLine("Consumer连接成功");

            ConsoleHelper.WriteLine("开始稳定性测试。。。");

            Random rd = new Random((int)DateTime.Now.Ticks);

            ActionResult<UserInfo> data = new ActionResult<UserInfo>()
            {
                Code = 200,
                Error = string.Empty,
                Success = true,
                Data = new UserInfo()
                {
                    ID = 1,
                    UserName = "yswenli",
                    Birthday = DateTime.Now
                }
            };

            while (true)
            {
                var r = cp.GenericService.Get(data);

                if (r != null)
                    ConsoleHelper.WriteLine("GenericService/Get/UserName:" + r.Data.UserName);

                System.Threading.Thread.Sleep(rd.Next(120000));
            }

        }

        private static void Cp_OnErr(string name, Exception ex)
        {
            ConsoleHelper.WriteLine($"{name}  {ex.Message}");
        }
    }
}
