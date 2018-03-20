using SAEA.Commom;
using SAEA.RedisSocket;
using System;

namespace SAEA.RedisSocketTest
{
    class Program
    {
        static void Main(string[] args)
        {
            ConsoleHelper.Title = "SAEA.RedisSocketTest";
            ConsoleHelper.WriteLine("输入ip:port连接RedisServer");

            var ipPort = ConsoleHelper.ReadLine();
            if (string.IsNullOrEmpty(ipPort))
            {
                ipPort = "127.0.0.1:6379";
            }
            RedisClient redisClient = new RedisClient(ipPort);
            redisClient.Connect(); 
            //redisClient.Connect("wenli"); 


            var info = redisClient.Info();
            if (info.Contains("NOAUTH Authentication required."))
            {
                while (true)
                {
                    ConsoleHelper.WriteLine("请输入redis连接密码");
                    var auth = ConsoleHelper.ReadLine();
                    if (string.IsNullOrEmpty(auth))
                    {
                        auth = "yswenli";
                    }
                    var a = redisClient.Auth(auth);
                    if (a.Contains("OK"))
                    {
                        break;
                    }
                    else
                    {
                        ConsoleHelper.WriteLine(a);
                    }
                }
            }

            //redisConnection.SlaveOf();

            //redisConnection.Ping();

            redisClient.Select(1);

            //ConsoleHelper.WriteLine(redisConnection.Type("key0"));

            ConsoleHelper.WriteLine("dbSize:{0}", redisClient.DBSize().ToString());


            RedisOperationTest(redisClient, true);
            ConsoleHelper.ReadLine();
        }

        private static void RedisOperationTest(object sender, bool status)
        {
            RedisClient redisClient = (RedisClient)sender;
            if (status)
            {
                ConsoleHelper.WriteLine("连接redis服务器成功！");

                #region key value

                ConsoleHelper.WriteLine("回车开始kv插值操作...");
                ConsoleHelper.ReadLine();
                for (int i = 0; i < 1000; i++)
                {
                    redisClient.GetDataBase().Set("key" + i, "val" + i);
                }
                //redisConnection.GetDataBase().Exists("key0");
                ConsoleHelper.WriteLine("kv插入完成...");

                ConsoleHelper.WriteLine("回车开始获取kv值操作...");
                ConsoleHelper.ReadLine();

                var keys = redisClient.GetDataBase().Keys().Data.ToArray(false, "\r\n");

                foreach (var key in keys)
                {
                    var val = redisClient.GetDataBase().Get(key);
                    ConsoleHelper.WriteLine("Get val:" + val);
                }
                ConsoleHelper.WriteLine("获取kv值完成...");

                ConsoleHelper.WriteLine("回车开始开始kv移除操作...");
                ConsoleHelper.ReadLine();
                foreach (var key in keys)
                {
                    redisClient.GetDataBase().Del(key);
                }
                ConsoleHelper.WriteLine("移除kv值完成...");
                #endregion


                #region hashset
                string hid = "wenli";

                ConsoleHelper.WriteLine("回车开始HashSet插值操作...");
                ConsoleHelper.ReadLine();
                for (int i = 0; i < 1000; i++)
                {
                    redisClient.GetDataBase().HSet(hid, "key" + i, "val" + i);
                }
                ConsoleHelper.WriteLine("HashSet插值完成...");

                ConsoleHelper.WriteLine("回车开始HashSet插值操作...");
                ConsoleHelper.ReadLine();
                var hkeys = redisClient.GetDataBase().GetHKeys(hid).Data.ToArray();
                foreach (var hkey in hkeys)
                {
                    var val = redisClient.GetDataBase().HGet(hid, hkey);
                    ConsoleHelper.WriteLine("HGet val:" + val.Data);
                }

                var hall = redisClient.GetDataBase().HGetAll("wenli");
                ConsoleHelper.WriteLine("HashSet查询完成...");

                ConsoleHelper.WriteLine("回车开始HashSet移除操作...");
                ConsoleHelper.ReadLine();
                foreach (var hkey in hkeys)
                {
                    redisClient.GetDataBase().HDel(hid, hkey);
                }
                ConsoleHelper.WriteLine("HashSet移除完成...");


                #endregion


                //redisConnection.GetDataBase().Suscribe((c, m) =>
                //{
                //    ConsoleHelper.WriteLine("channel:{0} msg:{1}", c, m);
                //    redisConnection.GetDataBase().UNSUBSCRIBE(c);
                //}, "c39654");


                ConsoleHelper.WriteLine("测试完成！");
            }
            else
            {
                ConsoleHelper.WriteLine("连接失败！");
            }
        }
    }
}
