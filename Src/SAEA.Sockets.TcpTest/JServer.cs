/****************************************************************************
*项目名称：SAEA.Sockets.TcpTest
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Sockets.TcpTest
*类 名 称：JServer
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/1/5 10:55:08
*描述：
*=====================================================================
*修改时间：2021/1/5 10:55:08
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using System;

using JT808.Protocol;

using SAEA.Sockets.Interface;
using SAEA.Sockets.Shortcut;

namespace SAEA.Sockets.TcpTest
{
    /// <summary>
    /// JT808 服务端演示：监听 39808 端口，按连接拆包并把解析结果以事件抛出
    /// </summary>
    public class JServer
    {
        TCPServer<JUnpacker> _server;

        /// <summary>
        /// 收到并解析出完整 JT808 包时触发，参数为当前服务端、连接标识与解析后的包
        /// </summary>
        public event Action<JServer, string, JT808Package> OnReceive;

        /// <summary>
        /// 创建监听 39808 端口的服务端并注册接入、断开、错误与接收回调
        /// </summary>
        public JServer()
        {
            _server = new TCPServer<JUnpacker>(39808);

            _server.OnAccept += _server_OnAccept;
            _server.OnDisconnect += _server_OnDisconnect;
            _server.OnError += _server_OnError;
            _server.OnReceive += _server_OnReceive;
        }

        private void _server_OnReceive(TCPServer<JUnpacker> arg1, IUserToken arg2, ReadOnlyMemory<byte> arg3)
        {
            var jUnpacker = (JUnpacker)arg2.Coder;
            var b=jUnpacker.Decode(arg3.ToArray());
            if (b == null) return;
            var package = new JT808Serializer().Deserialize<JT808Package>(b.AsSpan());
            OnReceive?.Invoke(this, arg2.ID, package);
        }

        private void _server_OnError(TCPServer<JUnpacker> arg1, string arg2, Exception arg3)
        {
            Console.WriteLine($"_server_OnError:{arg3.Message}");
        }

        private void _server_OnDisconnect(TCPServer<JUnpacker> arg1, string arg2, Exception arg3)
        {
            Console.WriteLine($"ServerSokcet_OnDisconnected:{arg2}");
        }

        private void _server_OnAccept(TCPServer<JUnpacker> arg1, string arg2)
        {
            Console.WriteLine($"ServerSokcet_OnAccepted:{arg2}");
        }

        /// <summary>
        /// 启动端口监听
        /// </summary>
        public void Start()
        {
            _server.Start();
        }

        /// <summary>
        /// 序列化 JT808 包后按连接标识提交发送，不等待底层发送完成
        /// </summary>
        /// <param name="id">目标连接的标识</param>
        /// <param name="jT808Package">待发送的 JT808 包</param>
        public void SendAsync(string id, JT808Package jT808Package)
        {
            _server.SendAsync(id, new ReadOnlyMemory<byte>(new JT808Serializer().Serialize(jT808Package)));
        }
    }
}
