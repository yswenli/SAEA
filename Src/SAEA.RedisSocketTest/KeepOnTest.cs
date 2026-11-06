using System;
using System.Collections.Generic;
using System.Text;

using SAEA.Common;
using SAEA.Common.Serialization;
using SAEA.RedisSocket;

namespace SAEA.RedisSocketTest
{
    /// <summary>
    /// 连接保活演示：反复触发命令观察长连接是否维持，异常时输出序列化后的异常信息
    /// </summary>
    public static class KeepOnTest
    {
        /// <summary>
        /// 连接本地 Redis 后循环等待回车并输出 INFO 结果，命令异常不退出循环
        /// </summary>
        public static void Test()
        {
            ConsoleHelper.WriteLine("KeepOnTest Start");

            RedisClient redisClient = new RedisClient("server=127.0.0.1:6379;passwords=yswenli");

            redisClient.Connect();

            Console.WriteLine(redisClient.Info());

            while (true)
            {
                ConsoleHelper.ReadLine();

                try
                {
                    Console.WriteLine(redisClient.Info());

                }
                catch(Exception ex)
                {
                    Console.WriteLine(SerializeHelper.Serialize(ex));
                }
            }
        }
    }
}
