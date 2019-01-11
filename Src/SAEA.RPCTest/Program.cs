using SAEA.Common;
using SAEA.RPC.Provider;
using SAEA.RPCTest.Consumer;
using SAEA.RPCTest.Consumer.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SAEA.RPCTest
{
    class Program
    {

        static int count = 100000;

        static string str = "  aBc希特勒于1935年3月废除《凡尔赛条约》后，德国军方胃口大增，对尚在定型中的III号和IV号坦克不再满足了。因为它们的装甲防护相对国外坦克不占优势，其平均装甲厚度为30毫米，而英、法坦克的平均装甲厚度为40～60毫米，开发新式重型坦克的工作势在必行。    aBc希特勒于1935年3月废除《凡尔赛条约》后，德国军方胃口大增，对尚在定型中的III号和IV号坦克不再满足了。因为它们的装甲防护相对国外坦克不占优势，其平均装甲厚度为30毫米，而英、法坦克的平均装甲厚度为40～60毫米，开发新式重型坦克的工作势在必行。    aBc希特勒于1935年3月废除《凡尔赛条约》后，德国军方胃口大增，对尚在定型中的III号和IV号坦克不再满足了。因为它们的装甲防护相对国外坦克不占优势，其平均装甲厚度为30毫米，而英、法坦克的平均装甲厚度为40～60毫米，开发新式重型坦克的工作势在必行。    aBc希特勒于1935年3月废除《凡尔赛条约》后，德国军方胃口大增，对尚在定型中的III号和IV号坦克不再满足了。因为它们的装甲防护相对国外坦克不占优势，其平均装甲厚度为30毫米，而英、法坦克的平均装甲厚度为40～60毫米，开发新式重型坦克的工作势在必行。  ";

        static string subStr = "《凡尔赛条约》";

        static string splitStr = "坦克";


        static void Main(string[] args)
        {

            ConsoleHelper.WriteLine($"SAEA.RPC测试： {Environment.NewLine}   a 启动rpc provider consumer{Environment.NewLine}   p 启动rpc provider{Environment.NewLine}   c 启动rpc consumer{Environment.NewLine}   g 启动rpc consumer代码生成器{Environment.NewLine}   t 启动rpc稳定性测试{Environment.NewLine}   s 启动rpc序列化测试");

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
                ConsoleHelper.WriteLine("ParamsSerializeUtil序列化测试中...");
                SerializeTest();
                ConsoleHelper.WriteLine("ParamsSerializeUtil序列化测试完毕，回车结束！");
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

            sp.OnErr += Sp_OnErr;

            sp.Start();

            ConsoleHelper.WriteLine("Provider就绪！");
        }

        private static void Sp_OnErr(Exception ex)
        {
            ConsoleHelper.WriteLine("Provider Error:" + ex.Message);
        }

        static void Generate()
        {
            RPC.Generater.CodeGnerater.Generate(PathHelper.Current, "SAEA.RPCTest");
        }

        static void ConsumerInit()
        {
            ConsoleHelper.Title = "SAEA.RPC.Consumer";

            var url = "rpc://127.0.0.1:39654";

            ConsoleHelper.WriteLine("请输入url");
            var input = ConsoleHelper.ReadLine();
            if (string.IsNullOrEmpty(input))
                input = url;


            ConsoleHelper.WriteLine($"Consumer正在连接到{url}...");

            RPCServiceProxy cp = new RPCServiceProxy(input, 1, 1, 1000000);
            cp.OnErr += Cp_OnErr;

            ConsoleHelper.WriteLine("Consumer连接成功");

            ConsoleHelper.WriteLine("HelloService/Hello:" + cp.HelloService.Hello().Length);
            ConsoleHelper.WriteLine("HelloService/Plus:" + cp.HelloService.Plus(1, 9));
            ConsoleHelper.WriteLine("HelloService/Update/UserName:" + cp.HelloService.Update(new Consumer.Model.UserInfo() { ID = 1, UserName = "yswenli" }).UserName);
            ConsoleHelper.WriteLine("HelloService/SendData:" + System.Text.Encoding.UTF8.GetString(cp.HelloService.SendData(System.Text.Encoding.UTF8.GetBytes("Hello Data"))));
            ConsoleHelper.WriteLine("");

            ConsoleHelper.WriteLine("GroupService/Add/ Creator.UserName:" + cp.GroupService.Add("rpc group", new Consumer.Model.UserInfo() { ID = 1, UserName = "yswenli" }).Creator.UserName);
            ConsoleHelper.WriteLine("GroupService/Update/Count:" + cp.GroupService.Update(new System.Collections.Generic.List<Consumer.Model.UserInfo>() { new Consumer.Model.UserInfo() { ID = 1, UserName = "yswenli" } }).Count);

            var groupInfoResult = cp.GroupService.GetGroupInfo(1);

            ConsoleHelper.WriteLine("GroupService/GetGroupInfo/Users.UserName:" + groupInfoResult.Users[0].UserName);
            ConsoleHelper.WriteLine("");

            var dic = new Dictionary<int, Consumer.Model.UserInfo>();
            dic.Add(1, new Consumer.Model.UserInfo() { UserName = "yswenli" });
            ConsoleHelper.WriteLine("DicService/Test/UserName:" + cp.DicService.Test(1, dic)[1].UserName);
            ConsoleHelper.WriteLine("");


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
            ConsoleHelper.WriteLine("GenericService/Get/UserName:" + cp.GenericService.Get(data).Data.UserName);
            ConsoleHelper.WriteLine("GenericService/GetListString/Count:" + cp.GenericService.GetListString().Count);
            ConsoleHelper.WriteLine("");

            Console.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}  EnumService/Get/GetEnum:" + cp.EnumService.GetEnum(EnumServiceType.Best).ToString());
            Console.WriteLine("");

            ConsoleHelper.WriteLine("回车启动性能测试！");

            ConsoleHelper.ReadLine();

            #region 性能测试

            Stopwatch sw = new Stopwatch();

            int count = 100000;

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


        public static byte[] SerializeBinary(object request)
        {

            System.Runtime.Serialization.Formatters.Binary.BinaryFormatter serializer =

            new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();

            using (System.IO.MemoryStream memStream = new System.IO.MemoryStream())
            {
                serializer.Serialize(memStream, request);

                return memStream.ToArray();
            }
        }


        public static object DeSerializeBinary(byte[] data)
        {
            using (System.IO.MemoryStream memStream = new System.IO.MemoryStream(data))
            {
                System.Runtime.Serialization.Formatters.Binary.BinaryFormatter deserializer =

                new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();

                return deserializer.Deserialize(memStream);
            }
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
            var len1 = 0;
            var len2 = 0;

            Stopwatch sw = new Stopwatch();
            sw.Start();

            List<byte[]> list = new List<byte[]>();
            for (int i = 0; i < count; i++)
            {
                var bytes = SerializeBinary(groupInfo);
                len1 = bytes.Length;
                list.Add(bytes);
            }
            ConsoleHelper.WriteLine($"BinaryFormatter实体序列化平均：{count * 1000 / sw.ElapsedMilliseconds} 次/秒");

            sw.Restart();
            for (int i = 0; i < count; i++)
            {
                var obj = DeSerializeBinary(list[i]);
            }
            ConsoleHelper.WriteLine($"BinaryFormatter实体反序列化平均：{count * 1000 / sw.ElapsedMilliseconds} 次/秒");
            ConsoleHelper.WriteLine($"BinaryFormatter序列化生成bytes大小：{len1 * count * 1.0 / 1024 / 1024} Mb");
            list.Clear();
            sw.Restart();

            for (int i = 0; i < count; i++)
            {
                var bytes = RPC.Serialize.ParamsSerializeUtil.Serialize(groupInfo);
                len2 = bytes.Length;
                list.Add(bytes);
            }
            ConsoleHelper.WriteLine($"ParamsSerializeUtil实体序列化平均：{count * 1000 / sw.ElapsedMilliseconds} 次/秒");
            sw.Restart();
            for (int i = 0; i < count; i++)
            {
                var obj = RPC.Serialize.ParamsSerializeUtil.Deserialize<GroupInfo>(list[i]);
            }
            ConsoleHelper.WriteLine($"ParamsSerializeUtil实体反序列化平均：{count * 1000 / sw.ElapsedMilliseconds} 次/秒");
            ConsoleHelper.WriteLine($"ParamsSerializeUtil序列化生成bytes大小：{len2 * count * 1.0 / 1024 / 1024} Mb");
            sw.Stop();
        }


        static void StabilityTest()
        {
            ProviderInit();

            ConsoleHelper.Title = "SAEA.RPC稳定性测试";

            var url = "rpc://127.0.0.1:39654";

            ConsoleHelper.WriteLine($"Consumer正在连接到{url}...");

            RPCServiceProxy cp = new RPCServiceProxy(url, 1, 1);
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
