/****************************************************************************
*项目名称：SAEA.RedisSocketTest
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.RedisSocketTest
*类 名 称：RedisStreamTest
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/1/12 16:29:28
*描述：
*=====================================================================
*修改时间：2021/1/12 16:29:28
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Threading;
using SAEA.RedisSocket;
using SAEA.RedisSocket.Core.Stream;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAEA.RedisSocketTest
{
    class RedisStreamTest
    {
        RedisClient _redisClient;

        public RedisStreamTest(RedisClient redisClient)
        {
            _redisClient = redisClient;
        }


        public void Test()
        {
            Console.WriteLine($"回车开始 RedisStream Test");
            Console.ReadLine();

            var topic = "mystream";

            var producer = _redisClient.GetRedisProducer();

            TaskHelper.LongRunning(() =>
            {
                producer.Publish(topic, $"date:{DateTimeHelper.Now:yyyy-MM-dd HH:mm:ss.fff}");
            }, 1000);

            var redisQueue = _redisClient.GetRedisQueue();
            var data = redisQueue.GetRange(topic, 3);

            using (var consumer1 = _redisClient.GetRedisConsumer(new List<TopicID>() { new TopicID(topic, "$") }))
            {
                var redisFilelds1 = consumer1.Subscribe();
            }


            using (var consumer2 = _redisClient.GetRedisConsumer(new List<TopicID>() { new TopicID(topic, "0") }, 2))
            {
                var redisFilelds2 = consumer2.Subscribe();
            }


            using (var consumer3 = _redisClient.GetRedisGroupConsumer("saea.redisscoket", "yswenli", topic, 1, true))
            {
                var redisFilelds3 = consumer3.SubscribeWithGroup();
                consumer3.RemoveGroup();
            }


            var consumer4 = _redisClient.GetRedisGroupConsumer("saea.redisscoket", "yswenli", topic, 1, false, "", false, true);
            consumer4.OnReceive += Consumer4_OnReceive;
            consumer4.OnError += Consumer4_OnError;
            consumer4.Start();



            Console.WriteLine($"RedisStream Test已完成");
            Console.ReadLine();

        }

        private void Consumer4_OnError(Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        private void Consumer4_OnReceive(RedisGroupConsumer consumer, IEnumerable<StreamEntry> obj)
        {
            var list = obj.ToList();

            Console.WriteLine($"{list.Count}");

            var ids = new List<RedisID>();

            foreach (var item in list)
            {
                var ilist = item.IdFileds;

                foreach (var sitem in ilist)
                {
                    ids.Add(sitem.RedisID);
                }
            }

            var c = consumer.Commit(ids);
        }
    }
}
