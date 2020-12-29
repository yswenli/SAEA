/****************************************************************************
*项目名称：SAEA.DNS
*CLR 版本：3.0
*机器名称：WENLI-PC
*命名空间：SAEA.DNS.Coder
*类 名 称：TcpRequestCoder
*版 本 号：v5.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/11/28 22:43:28
*描述：
*=====================================================================
*修改时间：2019/11/28 22:43:28
*修 改 人： yswenli
*版 本 号： v5.0.0.1
*描    述：
*****************************************************************************/
using SAEA.DNS.Protocol;
using SAEA.Sockets;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.DNS.Coder
{
    /// <summary>
    /// tcp模式处理编解码
    /// </summary>
    public class TcpRequestCoder : IRequestCoder
    {
        private IPEndPoint dns;

        /// <summary>
        /// tcp模式处理编解码
        /// </summary>
        /// <param name="dns"></param>
        public TcpRequestCoder(IPEndPoint dns)
        {
            this.dns = dns;
        }

        /// <summary>
        /// 编码处理
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<IResponse> Code(IRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            var socketOption = SocketOptionBuilder.Instance
                .UseStream()
                .SetIP(dns.Address.ToString())
                .SetPort(dns.Port)
                .Build();

            using (var socket = SocketFactory.CreateClientSocket(socketOption, cancellationToken))
            {
                socket.Connect();

                Stream stream = socket.GetStream();

                byte[] buffer = request.ToArray();

                byte[] length = BitConverter.GetBytes((ushort)buffer.Length);

                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(length);
                }

                await stream.WriteAsync(length, 0, length.Length, cancellationToken);

                await stream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);

                buffer = new byte[2];

                await Read(stream, buffer, cancellationToken);

                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(buffer);
                }

                buffer = new byte[BitConverter.ToUInt16(buffer, 0)];

                await Read(stream, buffer, cancellationToken);

                IResponse response = Protocol.DnsResponseMessage.FromArray(buffer);

                return new Model.DnsClientResponse(request, response, buffer);
            }
        }

        private static async Task Read(Stream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            int length = buffer.Length;
            int offset = 0;
            int size = 0;

            while (length > 0 && (size = await stream.ReadAsync(buffer, offset, length, cancellationToken)) > 0)
            {
                offset += size;
                length -= size;
            }

            if (length > 0)
            {
                throw new IOException("Unexpected end of stream");
            }
        }
    }
}
