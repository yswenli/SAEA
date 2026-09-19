/* SAEA.Socket5
 * 命名空间：SAEA.Socket5.Protocol
 * 文件名：Socks5Codec.cs
 * 版本号：v26.9.20.1
 * 描述：SOCKS5 字节级编解码（RFC1928 / RFC1929），所有读取基于 Stream 精确阻塞读取，天然兼容半包
 */

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

using SAEA.Socket5.Model;

namespace SAEA.Socket5.Protocol
{
    /// <summary>
    /// SOCKS5 协议编解码工具
    /// </summary>
    public static class Socks5Codec
    {
        private const byte Version5 = 0x05;

        private const byte UserPassVersion = 0x01;

        /// <summary>
        /// 从流中精确读取 count 个字节（循环读取以兼容半包），连接关闭时抛 EndOfStreamException
        /// </summary>
        public static byte[] ReadExactly(Stream stream, int count)
        {
            if (count <= 0) return new byte[0];

            var buffer = new byte[count];
            var offset = 0;

            while (offset < count)
            {
                var read = stream.Read(buffer, offset, count - offset);

                if (read == 0)
                {
                    throw new EndOfStreamException("SOCKS5 连接已关闭，无法读取足够的数据");
                }

                offset += read;
            }

            return buffer;
        }

        /// <summary>
        /// 根据主机字符串推断地址类型
        /// </summary>
        public static Socks5AddressType InferAddressType(string host)
        {
            if (IPAddress.TryParse(host, out var ip))
            {
                if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    return Socks5AddressType.IPv6;
                }

                return Socks5AddressType.IPv4;
            }

            return Socks5AddressType.Domain;
        }

        #region 方法协商

        /// <summary>
        /// 客户端：写出方法协商包 05 NMETHODS METHODS...
        /// </summary>
        public static void WriteMethodNegotiation(Stream stream, Socks5AuthMethod[] methods)
        {
            var ms = methods ?? new Socks5AuthMethod[0];
            var buf = new byte[2 + ms.Length];

            buf[0] = Version5;
            buf[1] = (byte)ms.Length;

            for (var i = 0; i < ms.Length; i++)
            {
                buf[2 + i] = (byte)ms[i];
            }

            stream.Write(buf, 0, buf.Length);
        }

        /// <summary>
        /// 服务端：读取方法协商包，返回客户端支持的方法
        /// </summary>
        public static Socks5AuthMethod[] ReadMethodNegotiation(Stream stream)
        {
            var header = ReadExactly(stream, 2);

            if (header[0] != Version5)
            {
                throw new InvalidDataException("不支持的 SOCKS 版本");
            }

            var n = header[1];
            var bytes = ReadExactly(stream, n);

            var methods = new Socks5AuthMethod[n];

            for (var i = 0; i < n; i++)
            {
                methods[i] = (Socks5AuthMethod)bytes[i];
            }

            return methods;
        }

        /// <summary>
        /// 服务端：写出方法选择包 05 METHOD
        /// </summary>
        public static void WriteMethodSelection(Stream stream, Socks5AuthMethod method)
        {
            stream.Write(new byte[] { Version5, (byte)method }, 0, 2);
        }

        /// <summary>
        /// 客户端：读取方法选择包，返回服务端选定的方法
        /// </summary>
        public static Socks5AuthMethod ReadMethodSelection(Stream stream)
        {
            var buf = ReadExactly(stream, 2);

            if (buf[0] != Version5)
            {
                throw new InvalidDataException("不支持的 SOCKS 版本");
            }

            return (Socks5AuthMethod)buf[1];
        }

        #endregion

        #region 用户名/密码认证（RFC1929）

        /// <summary>
        /// 客户端：写出用户名/密码认证包 01 ULEN UNAME PLEN PASSWD
        /// </summary>
        public static void WriteUserPassAuth(Stream stream, string userName, string password)
        {
            var u = Encoding.ASCII.GetBytes(userName ?? string.Empty);
            var p = Encoding.ASCII.GetBytes(password ?? string.Empty);

            var buf = new byte[1 + 1 + u.Length + 1 + p.Length];
            var i = 0;

            buf[i++] = UserPassVersion;
            buf[i++] = (byte)u.Length;
            Buffer.BlockCopy(u, 0, buf, i, u.Length); i += u.Length;
            buf[i++] = (byte)p.Length;
            Buffer.BlockCopy(p, 0, buf, i, p.Length); i += p.Length;

            stream.Write(buf, 0, buf.Length);
        }

        /// <summary>
        /// 服务端：读取用户名/密码认证包
        /// </summary>
        public static void ReadUserPassAuth(Stream stream, out string userName, out string password)
        {
            var header = ReadExactly(stream, 2);

            if (header[0] != UserPassVersion)
            {
                throw new InvalidDataException("不支持的用户名/密码认证版本");
            }

            var u = ReadExactly(stream, header[1]);
            var plen = ReadExactly(stream, 1)[0];
            var p = ReadExactly(stream, plen);

            userName = Encoding.ASCII.GetString(u);
            password = Encoding.ASCII.GetString(p);
        }

        /// <summary>
        /// 服务端：写出认证状态包 01 STATUS
        /// </summary>
        public static void WriteUserPassStatus(Stream stream, Socks5AuthStatus status)
        {
            stream.Write(new byte[] { UserPassVersion, (byte)status }, 0, 2);
        }

        /// <summary>
        /// 客户端：读取认证状态包
        /// </summary>
        public static Socks5AuthStatus ReadUserPassStatus(Stream stream)
        {
            var buf = ReadExactly(stream, 2);

            if (buf[0] != UserPassVersion)
            {
                throw new InvalidDataException("不支持的用户名/密码认证版本");
            }

            return (Socks5AuthStatus)buf[1];
        }

        #endregion

        #region 地址编解码

        /// <summary>
        /// 编码地址（含 ATYP + 地址 + 2 字节端口），不含 RSV/FRAG
        /// </summary>
        public static byte[] EncodeAddressBytes(Socks5AddressType addressType, string host, int port)
        {
            var list = new System.Collections.Generic.List<byte>();

            if (addressType == Socks5AddressType.Domain)
            {
                var d = Encoding.ASCII.GetBytes(host ?? string.Empty);
                list.Add((byte)Socks5AddressType.Domain);
                list.Add((byte)d.Length);
                list.AddRange(d);
            }
            else if (addressType == Socks5AddressType.IPv4)
            {
                var ip = IPAddress.Parse(host);
                list.Add((byte)Socks5AddressType.IPv4);
                list.AddRange(ip.GetAddressBytes());
            }
            else if (addressType == Socks5AddressType.IPv6)
            {
                var ip = IPAddress.Parse(host);
                list.Add((byte)Socks5AddressType.IPv6);
                list.AddRange(ip.GetAddressBytes());
            }
            else
            {
                throw new InvalidDataException("不支持的地址类型");
            }

            list.Add((byte)(port >> 8));
            list.Add((byte)(port & 0xFF));

            return list.ToArray();
        }

        /// <summary>
        /// 从流中读取地址（ATYP 已由调用方读取）
        /// </summary>
        public static void ReadAddress(Stream stream, Socks5AddressType addressType, out string host, out int port)
        {
            if (addressType == Socks5AddressType.IPv4)
            {
                var b = ReadExactly(stream, 4);
                host = new IPAddress(b).ToString();
            }
            else if (addressType == Socks5AddressType.IPv6)
            {
                var b = ReadExactly(stream, 16);
                host = new IPAddress(b).ToString();
            }
            else if (addressType == Socks5AddressType.Domain)
            {
                var len = ReadExactly(stream, 1)[0];
                var d = ReadExactly(stream, len);
                host = Encoding.ASCII.GetString(d);
            }
            else
            {
                throw new InvalidDataException("不支持的地址类型");
            }

            var pb = ReadExactly(stream, 2);
            port = (pb[0] << 8) | pb[1];
        }

        #endregion

        #region 请求 / 响应

        /// <summary>
        /// 客户端：写出请求包 05 CMD 00 ATYP ADDR PORT
        /// </summary>
        public static void WriteRequest(Stream stream, Socks5Command command, Socks5AddressType addressType, string host, int port)
        {
            // EncodeAddressBytes 已在 addr 起始处写入 ATYP，此处不要再重复写入，否则地址整体偏移 1 字节。
            var addr = EncodeAddressBytes(addressType, host, port);
            var buf = new byte[3 + addr.Length];

            buf[0] = Version5;
            buf[1] = (byte)command;
            buf[2] = 0x00;
            Buffer.BlockCopy(addr, 0, buf, 3, addr.Length);

            stream.Write(buf, 0, buf.Length);
        }

        /// <summary>
        /// 服务端：读取请求包
        /// </summary>
        public static Socks5Request ReadRequest(Stream stream)
        {
            var header = ReadExactly(stream, 4);

            if (header[0] != Version5)
            {
                throw new InvalidDataException("不支持的 SOCKS 版本");
            }

            var command = (Socks5Command)header[1];
            var addressType = (Socks5AddressType)header[3];

            ReadAddress(stream, addressType, out var host, out var port);

            return new Socks5Request
            {
                Command = command,
                AddressType = addressType,
                Host = host,
                Port = port
            };
        }

        /// <summary>
        /// 服务端：写出响应包 05 REP 00 ATYP BND.ADDR BND.PORT
        /// </summary>
        public static void WriteReply(Stream stream, Socks5ReplyCode replyCode, Socks5AddressType addressType, string host, int port)
        {
            // EncodeAddressBytes 已在 addr 起始处写入 ATYP，此处不要再重复写入，否则地址整体偏移 1 字节。
            var addr = EncodeAddressBytes(addressType, host, port);
            var buf = new byte[3 + addr.Length];

            buf[0] = Version5;
            buf[1] = (byte)replyCode;
            buf[2] = 0x00;
            Buffer.BlockCopy(addr, 0, buf, 3, addr.Length);

            stream.Write(buf, 0, buf.Length);
        }

        /// <summary>
        /// 客户端：读取响应包
        /// </summary>
        public static Socks5Reply ReadReply(Stream stream)
        {
            var header = ReadExactly(stream, 4);

            if (header[0] != Version5)
            {
                throw new InvalidDataException("不支持的 SOCKS 版本");
            }

            var replyCode = (Socks5ReplyCode)header[1];
            var addressType = (Socks5AddressType)header[3];

            ReadAddress(stream, addressType, out var host, out var port);

            return new Socks5Reply
            {
                ReplyCode = replyCode,
                AddressType = addressType,
                Host = host,
                Port = port
            };
        }

        #endregion

        #region UDP 数据报（RFC1928）

        /// <summary>
        /// 封装 UDP 数据报：RSV(2) FRAG(1) ATYP ADDR PORT DATA
        /// </summary>
        public static byte[] EncodeUdpDatagram(Socks5AddressType addressType, string host, int port, byte[] data)
        {
            var addr = EncodeAddressBytes(addressType, host, port);
            var buf = new byte[3 + addr.Length + data.Length];

            buf[0] = 0x00;
            buf[1] = 0x00;
            buf[2] = 0x00;
            Buffer.BlockCopy(addr, 0, buf, 3, addr.Length);
            Buffer.BlockCopy(data, 0, buf, 3 + addr.Length, data.Length);

            return buf;
        }

        /// <summary>
        /// 解析 UDP 数据报，去除 RSV/FRAG/ATYP/ADDR/PORT 头，得到目标地址与负载
        /// </summary>
        public static bool TryDecodeUdpDatagram(byte[] buffer, out Socks5AddressType addressType, out string host, out int port, out byte[] data)
        {
            addressType = Socks5AddressType.IPv4;
            host = null;
            port = 0;
            data = null;

            if (buffer == null || buffer.Length < 4)
            {
                return false;
            }

            // buffer[0..1] = RSV, buffer[2] = FRAG
            addressType = (Socks5AddressType)buffer[3];

            int index = 4;

            if (addressType == Socks5AddressType.IPv4)
            {
                if (buffer.Length < index + 4 + 2) return false;
                var ip = new IPAddress(SubArray(buffer, index, 4));
                host = ip.ToString();
                index += 4;
            }
            else if (addressType == Socks5AddressType.IPv6)
            {
                if (buffer.Length < index + 16 + 2) return false;
                var ip = new IPAddress(SubArray(buffer, index, 16));
                host = ip.ToString();
                index += 16;
            }
            else if (addressType == Socks5AddressType.Domain)
            {
                if (buffer.Length < index + 1) return false;
                var len = buffer[index];
                index += 1;
                if (buffer.Length < index + len + 2) return false;
                host = Encoding.ASCII.GetString(buffer, index, len);
                index += len;
            }
            else
            {
                return false;
            }

            if (buffer.Length < index + 2) return false;
            port = (buffer[index] << 8) | buffer[index + 1];
            index += 2;

            data = new byte[buffer.Length - index];
            Buffer.BlockCopy(buffer, index, data, 0, data.Length);

            return true;
        }

        #endregion

        private static byte[] SubArray(byte[] src, int index, int length)
        {
            var dst = new byte[length];
            Buffer.BlockCopy(src, index, dst, 0, length);
            return dst;
        }
    }
}
