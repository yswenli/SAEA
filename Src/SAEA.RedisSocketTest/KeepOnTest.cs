using System;
using System.Collections.Generic;
using System.Text;

using SAEA.Common;
using SAEA.Common.Serialization;
using SAEA.RedisSocket;

namespace SAEA.RedisSocketTest
{
    public static class KeepOnTest
    {
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
