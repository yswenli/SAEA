using JT808.Protocol;

using SAEA.Sockets.Core;
using SAEA.Sockets.Shortcut;

using System;
using System.Collections.Generic;
using System.Linq;

namespace SAEA.Sockets.TcpTest
{
    /// <summary>
    /// JT808 客户端演示的同步写法：直接读写 SocketStream，收到的包先入队再由 Receive 取出
    /// </summary>
    public class JClient2
    {
        JUnpacker _jUnpacker;

        TCPClient<JUnpacker> _client;

        List<JT808Package> _data;

        /// <summary>
        /// 创建指向 127.0.0.1:39808 的客户端，注册错误与断开回调并初始化接收队列
        /// </summary>
        public JClient2()
        {
            _jUnpacker = new JUnpacker();

            _client = new TCPClient<JUnpacker>("127.0.0.1", 39808);

            _client.OnError += _client_OnError;

            _client.OnDisconnect += _client_OnDisconnect;

            _data = new List<JT808Package>();
        }

        private void _client_OnDisconnect(TCPClient<JUnpacker> arg1, Exception arg2)
        {
            Console.WriteLine($"ClientSokcet_OnDisconnected:{arg1}");
        }

        private void _client_OnError(TCPClient<JUnpacker> arg1, Exception arg2)
        {
            Console.WriteLine($"ClientSokcet_OnError:{arg2.Message}");
        }

        /// <summary>
        /// 连接到服务端，失败由内部错误回调输出
        /// </summary>
        public void Connect()
        {
            _client.Connect();
        }

        /// <summary>
        /// 断开与服务端的连接，随后触发断开回调
        /// </summary>
        public void Disconnect()
        {
            _client.Disconnect();
        }


        /// <summary>
        /// 序列化 JT808 包后同步写入底层流，写入期间阻塞当前线程
        /// </summary>
        /// <param name="jT808Package">待发送的 JT808 包</param>
        public void Send(JT808Package jT808Package)
        {
            var data = new JT808Serializer().Serialize(jT808Package);
            _client.SocketStream.Write(data, 0, data.Length);
        }


        /// <summary>
        /// 同步阻塞读取并拆包：队列中已有包时直接返回队首，否则从流中读取一个完整帧
        /// </summary>
        /// <returns>下一个 JT808 包；流读到结尾时返回 null</returns>
        public JT808Package Receive()
        {
            do
            {
                if (_data.Count > 0)
                {
                    var result = _data.First();
                    _data.Remove(result);
                    return result;
                }
                var buffer = new byte[1024];
                var rlen = _client.SocketStream.Read(buffer, 0, 1024);
                if (rlen > 0)
                {
                    var data = buffer.AsSpan().Slice(0, rlen).ToArray();
                    var b = _jUnpacker.Decode(data);
                    if (b == null)
                    {
                        continue;
                    }
                    var package = new JT808Serializer().Deserialize<JT808Package>(b.AsSpan());
                    _data.Add(package);
                }
                else
                {
                    return null;
                }
            }
            while (true);
        }

    }
}
