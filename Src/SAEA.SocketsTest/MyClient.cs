/****************************************************************************
*项目名称：SAEA.SocketsTest
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.SocketsTest
*类 名 称：MyClient
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/8/19 15:04:00
*描述：
*=====================================================================
*修改时间：2019/8/19 15:04:00
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using JT808.Protocol;
using SAEA.Sockets;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.SocketsTest
{
    class MyClient
    {
        IClientSocket _client;

        MyContext _myContext;

        ConcurrentQueue<string> _ids = new ConcurrentQueue<string>();

        ConcurrentDictionary<string, JT808Package> _cache = new ConcurrentDictionary<string, JT808Package>();


        public MyClient()
        {
            _myContext = new MyContext();

            var option = SocketOptionBuilder.Instance.UseIocp(_myContext)
                .SetIP("127.0.0.1")
                .SetPort(39654)
                .Build();

            _client = SocketFactory.CreateClientSocket(option);

            _client.OnReceive += _client_OnReceive;
        }


        private void _client_OnReceive(byte[] data)
        {
            ((MyUnpacker)_myContext.Unpacker).Unpack(data, (package) =>
            {
                if (_ids.TryDequeue(out string guid))
                {
                    _cache[guid] = package;
                }
            });
        }


        public void Connect()
        {
            _client.ConnectAsync();
        }


        JT808Package BaseRequest(Func<byte[]> fun)
        {
            var guid = Guid.NewGuid().ToString("N");

            _ids.Enqueue(guid);

            var data = fun.Invoke();

            _client.Send(data);

            JT808Package result = null;

            var task = Task.Run(() =>
            {
                while (true)
                {
                    if (!_cache.TryRemove(guid, out result))
                    {
                        Thread.Sleep(1);
                    }
                    else
                    {
                        break;
                    }
                }
            });

            task.GetAwaiter().GetResult();

            return result;
        }

        public JT808Package Sign(string code)
        {
            return BaseRequest(() => ((MyUnpacker)_myContext.Unpacker).Sign(code));
        }

        public JT808Package ReportPosition()
        {
            return BaseRequest(() => ((MyUnpacker)_myContext.Unpacker).ReportPosition());
        }
    }
}
