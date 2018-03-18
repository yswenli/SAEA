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
            RedisConnection redisConnection = new RedisConnection(ipPort);
            redisConnection.Connect();
            redisConnection.Auth("yswenli");           
            redisConnection.GetDataBase().Suscribe((c, m) =>
            {
                ConsoleHelper.WriteLine("channel:{0} msg:{1}", c, m);
                redisConnection.GetDataBase().UNSUBSCRIBE(c);
            }, "c39654");
            var info = redisConnection.Info();
            redisConnection.SlaveOf();
            //redisConnection.Ping();
            redisConnection.Select(1);
            ConsoleHelper.WriteLine(redisConnection.Type("wenli"));
            ConsoleHelper.WriteLine(redisConnection.DBSize().ToString());
            RedisOperationTest(redisConnection, true);
            ConsoleHelper.ReadLine();
        }

        private static void RedisOperationTest(object sender, bool status)
        {
            RedisConnection redisConnection = (RedisConnection)sender;
            if (status)
            {
                ConsoleHelper.WriteLine("连接redis服务器成功！");

                #region key value

                ConsoleHelper.WriteLine("回车开始kv插值操作...");
                ConsoleHelper.ReadLine();
                for (int i = 0; i < 1000; i++)
                {
                    redisConnection.GetDataBase().Set("key" + i, "val" + i);
                }
                //redisConnection.GetDataBase().Exists("key0");
                ConsoleHelper.WriteLine("kv插入完成...");

                ConsoleHelper.WriteLine("回车开始获取kv值操作...");
                ConsoleHelper.ReadLine();

                var keys = redisConnection.GetDataBase().Keys().Data.ToArray(false, "\r\n");

                foreach (var key in keys)
                {
                    var val = redisConnection.GetDataBase().Get(key);
                    ConsoleHelper.WriteLine("Get val:" + val);
                }
                ConsoleHelper.WriteLine("获取kv值完成...");

                ConsoleHelper.WriteLine("回车开始开始kv移除操作...");
                ConsoleHelper.ReadLine();
                foreach (var key in keys)
                {
                    redisConnection.GetDataBase().Del(key);
                }
                ConsoleHelper.WriteLine("移除kv值完成...");
                #endregion


                #region hashset
                string hid = "wenli";

                ConsoleHelper.WriteLine("回车开始HashSet插值操作...");
                ConsoleHelper.ReadLine();
                for (int i = 0; i < 1000; i++)
                {
                    redisConnection.GetDataBase().HSet(hid, "key" + i, "val" + i);
                }
                ConsoleHelper.WriteLine("HashSet插值完成...");

                ConsoleHelper.WriteLine("回车开始HashSet插值操作...");
                ConsoleHelper.ReadLine();
                var hkeys = redisConnection.GetDataBase().GetHKeys(hid).Data.ToArray();
                foreach (var hkey in hkeys)
                {
                    var val = redisConnection.GetDataBase().HGet(hid, hkey);
                    ConsoleHelper.WriteLine("HGet val:" + val.Data);
                }

                var hall = redisConnection.GetDataBase().HGetAll("wenli");
                ConsoleHelper.WriteLine("HashSet查询完成...");

                ConsoleHelper.WriteLine("回车开始HashSet移除操作...");
                ConsoleHelper.ReadLine();
                foreach (var hkey in hkeys)
                {
                    redisConnection.GetDataBase().HDel(hid, hkey);
                }
                ConsoleHelper.WriteLine("HashSet移除完成...");


                #endregion



                ConsoleHelper.WriteLine("测试完成！");
            }
            else
            {
                ConsoleHelper.WriteLine("连接失败！");
            }
        }
    }
}
