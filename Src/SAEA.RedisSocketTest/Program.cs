/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RedisSocketTest
*文件名： Program
*版本号： V2.1.5.0
*唯一标识：3d4f939c-3fb9-40e9-a0e0-c7ec773539ae
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/17 10:37:15
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/19 10:37:15
*修改人： yswenli
*版本号： V2.1.5.0
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.RedisSocket;
using System;

namespace SAEA.RedisSocketTest
{
    class Program
    {
        static void Main(string[] args)
        {
            ConsoleHelper.Title = "SAEA.RedisSocketTest";
            ConsoleHelper.WriteLine("输入连接字符串连接RedisServer，格式为\r\nserver=127.0.0.1:6379;passwords=yswenli");

            var cnnStr = ConsoleHelper.ReadLine();
            if (string.IsNullOrEmpty(cnnStr))
            {
                //cnnStr = "server=127.0.0.1:6380;passwords=yswenli";
                cnnStr = "server=172.31.32.85:6379;passwords=yswenli";
            }
            RedisClient redisClient = new RedisClient(cnnStr);
            redisClient.Connect();

            //var keys = redisClient.GetDataBase().Keys();

            //var scan = redisClient.GetDataBase().Scan();
            //var hscan = redisClient.GetDataBase().HScan("haa22", 0);
            //var sscan = redisClient.GetDataBase().SScan("aaa", 0);
            //var zscan = redisClient.GetDataBase().ZScan("zaaa", 0);

            //var r = redisClient.GetDataBase().Rename("aaa", "aaa");

            //var l = redisClient.GetDataBase().LRang("testlist");

            //var z = redisClient.GetDataBase().ZRang("zaaa");

            //var h = redisClient.GetDataBase().HGetAll("haa22");

            redisClient.Select(0);

            //var m = redisClient.ClusterInfo;
            //var n = redisClient.ClusterNodes;
            //var k = redisClient.KeySlot("aaa");
            //var g = redisClient.GetKeysInSlot(0);

            //redisClient.GetDataBase().SRemove("abcd", "12345");

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

            //redisClient.Select(1);

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
                ConsoleHelper.WriteLine("回车开始kv插值次操作...");
                //ConsoleHelper.WriteLine("回车开始kv插值1000000次操作...");
                //ConsoleHelper.ReadLine();
                //for (int i = 0; i < 1000000; i++)
                //{
                //    redisClient.GetDataBase().Set("key" + i, "val" + i);
                //}
                //ConsoleHelper.WriteLine("kv插值操作已完成");
                //ConsoleHelper.ReadLine();
                //return;
                //for (int i = 0; i < 100000; i++)
                //{
                //    redisClient.GetDataBase().Del("key" + i);
                //}
                for (int i = 0; i < 100; i++)
                {
                    redisClient.GetDataBase().Set("key" + i, "val" + i);
                }
                //redisConnection.GetDataBase().Exists("key0");
                ConsoleHelper.WriteLine("kv插入完成...");

                ConsoleHelper.WriteLine("回车开始获取kv值操作...");
                ConsoleHelper.ReadLine();

                var keys = redisClient.GetDataBase().Keys();

                foreach (var key in keys)
                {
                    var val = redisClient.GetDataBase().Get(key);
                    ConsoleHelper.WriteLine("Get val:" + val);
                }
                ConsoleHelper.WriteLine("获取kv值完成...");

                ConsoleHelper.WriteLine("回车开始开始kv移除操作...");
                ConsoleHelper.ReadLine();
                for (int i = 0; i < 100; i++)
                {
                    redisClient.GetDataBase().Del("key" + i);
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
                var hkeys = redisClient.GetDataBase().GetHKeys(hid);
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
