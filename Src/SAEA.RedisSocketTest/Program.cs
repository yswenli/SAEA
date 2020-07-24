/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocketTest
*文件名： Program
*版本号： v5.0.0.1
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
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.RedisSocket;
using SAEA.RedisSocket.Core;
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;

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
                cnnStr = "server=127.0.0.1:6379;passwords=yswenli";
            }
            RedisClient redisClient = new RedisClient(cnnStr, false);

            redisClient.Connect();

            redisClient.GetDataBase().HSetAsync(TimeSpan.FromSeconds(5), "hid", "key", "val");
            var r = redisClient.GetDataBase().HGetAsync(TimeSpan.FromSeconds(5), "hid", "key").Result;
            var rr = redisClient.GetDataBase().HDelAsync(TimeSpan.FromSeconds(5), "hid", "key").Result;

            var rk = redisClient.GetDataBase().RandomKey();

            var crk = redisClient.Console("RandomKey");

            var type = redisClient.Type("FUND_GROUP_TG_5c8abef4c30c6b9a");
            ConsoleHelper.WriteLine(type);
            var type1 = redisClient.Type("FUND_GROUP_TG_176d0049714c618a");
            ConsoleHelper.WriteLine(type1);
            var type2 = redisClient.Type("POINT_GROUP_PG_ad011ad469744a6bbc79981c1d0811d6");
            ConsoleHelper.WriteLine(type2);
            var type3 = redisClient.Type("FUND_GROUP_TG_48fb4e9e8d6dda15");
            ConsoleHelper.WriteLine(type3);
            var type4 = redisClient.Type("POINT_GROUP_PG_8136d029050740acab7a3d24a6e88663");
            ConsoleHelper.WriteLine(type4);

            ConsoleHelper.ReadLine();
            KeysTest(redisClient);

            var db = redisClient.GetDataBase(0);

            StringTest(db);

            ConsoleHelper.ReadLine();

            HashTest(db);

            ListTest(db);

            SetTest(db);

            GeoTest(db);

            ConsoleHelper.ReadLine();
        }

        static void KeysTest(RedisClient redisClient)
        {
            //var a = redisClient.Auth("yswenli");

            var info = redisClient.Info();

            var serverInfo = redisClient.ServerInfo;

            //redisClient.Select(1);

            var dbsize = redisClient.DBSize();

            redisClient.SlaveOf();

            var pong = redisClient.Ping();

            var clist = redisClient.ClientList();

            var ck = "slowlog-max-len";

            //var cr1 = redisClient.SetConfig(ck, 1000);

            var cr2 = redisClient.GetConfig(ck);

            var t1 = redisClient.GetDataBase().Ttl("zaaa");
            var t2 = redisClient.GetDataBase().Ttl("haa22");
            var t3 = redisClient.GetDataBase().Pttl("key0");
            var t4 = redisClient.GetDataBase().Pttl("akey0");

            var isCluster = redisClient.IsCluster;

        }

        static void StringTest(RedisDataBase db)
        {
            db.Set("yswenli", "good man");

            var val = db.Get("yswenli");

            var keys = db.Keys();

            db.Del("yswenli");

            var e = db.Exists("yswenli");

            db.Expire("saea", 60);

            db.Set("saea", "redis");

            db.ExpireAt("saea", DateTimeHelper.Now.AddSeconds(30));

            var t1 = db.Ttl("saea");

            db.Append("saea", " socket nibility");

            var v = db.Get("saea");

            var v1 = db.GetSet("saea", "redis socket");

            var r = db.RandomKey();

            db.Persist("saea");

            var t2 = db.Ttl("saea");

            var i1 = db.Increment("inc1");
            i1 = db.Increment("inc1");
            i1 = db.Increment("inc1");

            i1 = db.Decrement("inc1");
            i1 = db.Decrement("inc1");
            i1 = db.Decrement("inc1");

            var i2 = db.IncrementBy("inc2", 1);
            i2 = db.IncrementBy("inc2", 1);
            i2 = db.IncrementBy("inc2", 1);

            i2 = db.DecrementBy("inc2", 1);
            i2 = db.DecrementBy("inc2", 1);
            i2 = db.DecrementBy("inc2", 1);

            var i3 = db.IncrementByFloat("inc3", 0.1F);
            i3 = db.IncrementByFloat("inc3", 0.1F);
            i3 = db.IncrementByFloat("inc3", 0.1F);

            i3 = db.IncrementByFloat("inc3", -0.1F);
            i3 = db.IncrementByFloat("inc3", -0.1F);
            i3 = db.IncrementByFloat("inc3", -0.1F);

            var len = db.Len("saea");
        }

        static void HashTest(RedisDataBase db)
        {
            db.HSet("yswenliH", "saea1", "socket");

            var dic = new Dictionary<string, string>();
            dic.Add("saea2", "mvc");
            dic.Add("saea3", "rpc");
            db.HMSet("yswenliH", dic);

            var v1 = db.HGet("yswenliH", "saea1");

            var v2 = db.HMGet("yswenliH", new List<string>() { "saea1", "saea2", "saea3" });

            var v3 = db.HGetAll("yswenliH");

            var v4 = db.HGetKeys("yswenliH");

            var v5 = db.HGetValues("yswenliH");

            var l1 = db.HLen("yswenliH");

            var l2 = db.HStrLen("yswenliH", "saea1");

            var s = db.HScan("yswenliH");

            var b1 = db.HExists("yswenliH", "saea1");

            var b2 = db.HExists("yswenliH", "saea4");

            db.HDel("yswenliH", "saea1");

            db.HDel("yswenliH", new string[] { "saea2", "saea3" });

            v4 = db.HGetKeys("yswenliH");

            db.Del("yswenliH");

            var i1 = db.HIncrementBy("yswenliH", "inc", 1);

            i1 = db.HIncrementBy("yswenliH", "inc", 1);

            i1 = db.HIncrementBy("yswenliH", "inc", -1);

            i1 = db.HIncrementBy("yswenliH", "inc", -1);

            var i2 = db.HIncrementByFloat("yswenliH", "inc1", 0.1F);

            i2 = db.HIncrementByFloat("yswenliH", "inc1", 0.1F);

            i2 = db.HIncrementByFloat("yswenliH", "inc1", -0.1F);

            i2 = db.HIncrementByFloat("yswenliH", "inc1", -0.1F);

        }

        static void ListTest(RedisDataBase db)
        {
            var key = "yswenliL";

            var i1 = db.LPush(key, "saea");

            var l1 = db.LLen(key);

            var i2 = db.LPush(key, new List<string>() { "redis", "socket" });

            var l2 = db.LLen(key);

            var i3 = db.LPushX(key, "a");
            db.LPushX(key, "a");

            var l3 = db.LLen(key);

            var i4 = db.RPush(key, "b");

            var l4 = db.LLen(key);

            var i5 = db.RPush(key, new List<string>() { "c", "d", "e" });

            var v6 = db.LInsert(key, "c", true, "f");

            var l5 = db.LLen(key);

            var i6 = db.RPushX(key, "f");

            var l6 = db.LLen(key);

            var v1 = db.LPop(key);

            var l7 = db.LLen(key);

            var v2 = db.BLPop(key, 10);

            var l8 = db.LLen(key);

            var v3 = db.BLPop(new List<string>() { key, "key1" }, 10);

            var l9 = db.LLen(key);

            var v4 = db.RpopLPush(key, "key1");

            var l10 = db.LLen(key);

            var l11 = db.LLen("key1");

            var v5 = db.LIndex(key, 0);

            var v7 = db.RPop(key);

            db.LSet(key, 0, "redis1");

            var v8 = db.LRang(key);

            var v9 = db.LTrim(key, 1, 3);

            var v10 = db.BRPop(key, 10);

            var v11 = db.BRPopLPush(key, "key1", 10);

            var v12 = db.LRemove(key, 100, "a");
        }

        static void SetTest(RedisDataBase db)
        {
            var key = "yswenliS";

            db.SAdd(key, "saea");

            db.SAdd(key, new string[] { "redis", "socket", "a", "b", "c", "d", "e", "f" });

            var e = db.SExists(key, "saea");

            var v1 = db.SPop(key);

            var v2 = db.SRandMemeber(key);

            var s = db.SRemove(key, "saea");

            var i1 = db.SMove(key, "yswenliS1", "redis");

            var l1 = db.SLen(key);

            var l2 = db.SMemebers("yswenliS1");

            var l3 = db.SInter(key, "yswenliS1");

            var i2 = db.SInterStore("yswenliS2", key, "yswenliS1");

            var l4 = db.SUnion(key, "yswenliS1");

            var i3 = db.SUnionStore("yswenliS3", key, "yswenliS1");

            var l5 = db.SDiff(key, "yswenliS1");

            var i4 = db.SDiffStore("yswenliS4", key, "yswenliS1");
        }

        static void ZSetTest(RedisDataBase db)
        {
            var key = "ysweliZ";
            db.ZAdd(key, "aaa", 11);


            var z = db.ZRange("zaaa");
        }

        static void GeoTest(RedisDataBase db)
        {
            db.GeoAdd("yswenliG", new GeoItem() { Name = "Palermo", Lng = 13.361389, Lat = 38.115556 }, new GeoItem() { Name = "Catania", Lng = 15.087269, Lat = 37.502669 });

            var list = db.GeoPos("yswenliG", "Palermo", "Catania");

            var dis = db.GeoDist("yswenliG", "Palermo", "Catania");

            var ms1 = db.GeoRandius("yswenliG", 15, 37, 200, GeoUnit.km);

            var ms2 = db.GeoRandiusByMember("yswenliG", "Palermo", 200);

        }

        static void ScanTest(RedisDataBase db)
        {
            var scan = db.Scan();
            var hscan = db.HScan("haa22", 0);
            var sscan = db.SScan("aaa", 0);
            db.ZAdd("zaaa", "!#@%$^&*\r\n()^^%%&%@FSDH\r\n某月霜\r\n/.';lakdsfakdsf", 110);
            var zscan = db.ZScan("zaaa", 0);

            var zc = db.ZCount("zaaa");
            var zl = db.ZLen("zaaa");
        }

        static void ClusterTest(RedisClient redisClient)
        {
            var list = redisClient.ClusterNodes;

            var m = redisClient.ClusterInfo;

            var n = redisClient.ClusterNodes;

            var k = redisClient.KeySlot("aaa");

            var g = redisClient.GetKeysInSlot(0);
        }
    }
}

