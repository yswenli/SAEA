/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.Socket5.Client
*文件名： Socks5UdpClient
*版本号： v26.11.5.1
*唯一标识：55251d21-96da-4d47-b33d-0c768f9cf1ae
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/9/20 02:53:58
*描述：SOCKS5 UDP ASSOCIATE 客户端
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 13:11:19
*修改人： yswenli
*版本号： v26.11.5.1
*描述：规范文件头 banner
*
*****************************************************************************/
/* SAEA.Socket5
 * 命名空间：SAEA.Socket5.Client
 * 文件名：Socks5UdpClient.cs
 * 版本号：v26.9.20.1
 * 描述：经 SOCKS5 代理收发的 UDP 数据报客户端（UDP ASSOCIATE）
 */

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

using SAEA.Socket5.Model;
using SAEA.Socket5.Protocol;

namespace SAEA.Socket5.Client
{
    /// <summary>
    /// 经 SOCKS5 代理收发 UDP 数据报的客户端（UDP ASSOCIATE）
    /// </summary>
    public class Socks5UdpClient : IDisposable
    {
        private readonly Socks5Client _owner;

        private readonly UdpClient _udp;

        private readonly IPEndPoint _relayEndPoint;

        /// <summary>
        /// 构造
        /// </summary>
        /// <param name="owner">持有 TCP 控制连接的客户端（用于保持关联存活）</param>
        /// <param name="udp">本地 UDP 套接字</param>
        /// <param name="relayEndPoint">代理 UDP 中继端点</param>
        public Socks5UdpClient(Socks5Client owner, UdpClient udp, IPEndPoint relayEndPoint)
        {
            _owner = owner;
            _udp = udp ?? throw new ArgumentNullException(nameof(udp));
            _relayEndPoint = relayEndPoint ?? throw new ArgumentNullException(nameof(relayEndPoint));
        }

        /// <summary>
        /// 代理 UDP 中继端点
        /// </summary>
        public IPEndPoint RelayEndPoint => _relayEndPoint;

        /// <summary>
        /// 向目标主机发送 UDP 数据报（自动封装 SOCKS5 UDP 头）
        /// </summary>
        /// <param name="host">目标主机名或 IP 字符串</param>
        /// <param name="port">目标端口</param>
        /// <param name="data">UDP 负载，不能为 null</param>
        public void SendTo(string host, int port, byte[] data)
        {
            var addressType = Socks5Codec.InferAddressType(host);
            var datagram = Socks5Codec.EncodeUdpDatagram(addressType, host, port, data);
            _udp.Send(datagram, datagram.Length, _relayEndPoint);
        }

        /// <summary>
        /// 向目标主机发送文本（UTF-8 编码）
        /// </summary>
        /// <param name="host">目标主机名或 IP 字符串</param>
        /// <param name="port">目标端口</param>
        /// <param name="text">待发送文本，按 UTF-8 编码</param>
        public void SendTo(string host, int port, string text)
        {
            SendTo(host, port, Encoding.UTF8.GetBytes(text));
        }

        /// <summary>
        /// 接收并解析一个 UDP 数据报（去除 SOCKS5 UDP 头）
        /// </summary>
        /// <param name="host">解析出的源主机名或 IP 字符串</param>
        /// <param name="port">解析出的源端口</param>
        /// <param name="data">去掉头部后的负载</param>
        /// <param name="timeoutMs">接收超时（毫秒），默认 5000</param>
        /// <returns>true 表示收到并解析成功；超时或解析失败为 false</returns>
        public bool TryReceive(out string host, out int port, out byte[] data, int timeoutMs = 5000)
        {
            host = null;
            port = 0;
            data = null;

            _udp.Client.ReceiveTimeout = timeoutMs;

            try
            {
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                var buffer = _udp.Receive(ref remoteEP);
                return Socks5Codec.TryDecodeUdpDatagram(buffer, out _, out host, out port, out data);
            }
            catch (SocketException)
            {
                return false;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            try
            {
                _udp.Close();
            }
            catch { }

            _owner?.Dispose();
        }
    }
}
