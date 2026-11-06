/****************************************************************************
*项目名称：SAEA.Sockets.TcpTest
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Sockets.TcpTest
*类 名 称：JClient
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/1/5 10:55:21
*描述：
*=====================================================================
*修改时间：2021/1/5 10:55:21
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using System;

using JT808.Protocol;

using SAEA.Sockets.Shortcut;

namespace SAEA.Sockets.TcpTest
{
    /// <summary>
    /// JT808 客户端演示：用 JUnpacker 从接收字节中拆出完整帧，反序列化后以事件抛给调用方
    /// </summary>
    public class JClient
    {
        JUnpacker _jUnpacker;

        TCPClient<JUnpacker> _client;

        /// <summary>
        /// 收到并解析出完整 JT808 包时触发，参数为当前客户端与解析后的包
        /// </summary>
        public event Action<JClient, JT808Package> OnReceive;

        /// <summary>
        /// 创建指向 127.0.0.1:39808 的客户端并注册接收、断开与错误回调
        /// </summary>
        public JClient()
        {
            _jUnpacker = new JUnpacker();

            _client = new TCPClient<JUnpacker>("127.0.0.1", 39808);

            _client.OnError += _client_OnError;

            _client.OnDisconnect += _client_OnDisconnect;

            _client.OnReceive += _client_OnReceive;
        }

        private void _client_OnReceive(TCPClient<JUnpacker> arg1, ReadOnlyMemory<byte> arg2)
        {
            var b = _jUnpacker.Decode(arg2.ToArray());
            if (b == null) return;
            var package = new JT808Serializer().Deserialize<JT808Package>(b.AsSpan());
            OnReceive.Invoke(this, package);
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
        /// 序列化 JT808 包后提交发送，不等待底层发送完成
        /// </summary>
        /// <param name="jT808Package">待发送的 JT808 包</param>
        public void SendAsync(JT808Package jT808Package)
        {
            _client.SendAsync(new ReadOnlyMemory<byte>(new JT808Serializer().Serialize(jT808Package)));
        }
    }
}
