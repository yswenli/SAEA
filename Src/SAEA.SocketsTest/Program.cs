using System;

namespace SAEA.SocketsTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var str = "SAEA.Sockets JT808接入测试";

            Console.Title = str;

            Console.WriteLine(str);

            MyServer myServer = new MyServer();

            myServer.OnReceived += MyServer_OnReceived;

            myServer.Start();


            MyClient myClient = new MyClient();

            myClient.Connect();

            var result1 = myClient.Sign("this is a test!");

            Console.WriteLine($"{DateTime.Now.ToLongTimeString()} Client收到数据:{result1.Header.MsgId}");

            var result2 = myClient.ReportPosition();

            Console.WriteLine($"{DateTime.Now.ToLongTimeString()} Client收到数据:{result2.Header.MsgId}");

            Console.ReadLine();

        }

        private static void MyServer_OnReceived(JT808.Protocol.JT808Package obj)
        {
            Console.WriteLine($"{DateTime.Now.ToLongTimeString()} Server收到数据:{obj.Header.MsgId}");
        }
    }
}
