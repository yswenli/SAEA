using System;
using System.Collections.Generic;
using System.IO;
using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// 旧 BaseCoder 解码算法的测试副本，仅用于新旧结果逐字节对比。
    /// </summary>
    internal static class LegacyDecoder
    {
        public static List<ISocketProtocal> Decode(byte[] data, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
        {
            var result = new List<ISocketProtocal>();
            using (var buffer = new MemoryStream())
            {
                buffer.Write(data, 0, data.Length);
                buffer.Position = 0;

                while (buffer.Length - buffer.Position >= BaseCoder.P_Head)
                {
                    buffer.Position = 0;
                    var lenBytes = new byte[BaseCoder.P_LEN];
                    buffer.Read(lenBytes, 0, BaseCoder.P_LEN);
                    long bodyLen = BitConverter.ToInt64(lenBytes, 0);

                    buffer.Position = BaseCoder.P_LEN;
                    var type = (SocketProtocalType)buffer.ReadByte();
                    buffer.Position = 0;

                    if (bodyLen == 0 && type == SocketProtocalType.Heart)
                    {
                        Remove(buffer, BaseCoder.P_Head);
                        onHeart?.Invoke(DateTime.Now);
                    }
                    else if (buffer.Length >= BaseCoder.P_Head + bodyLen)
                    {
                        byte[] content;
                        if (bodyLen <= 0)
                        {
                            content = Array.Empty<byte>();
                        }
                        else
                        {
                            content = new byte[(int)bodyLen];
                            buffer.Position = BaseCoder.P_Head;
                            buffer.Read(content, 0, (int)bodyLen);
                            buffer.Position = 0;
                        }

                        if (type == SocketProtocalType.BigData)
                        {
                            onFile?.Invoke(content);
                        }
                        else
                        {
                            result.Add(new BaseSocketProtocal { BodyLength = bodyLen, Type = (byte)type, Content = content });
                        }

                        Remove(buffer, (int)(BaseCoder.P_Head + bodyLen));
                    }
                    else
                    {
                        buffer.Position = buffer.Length;
                        break;
                    }
                }
            }
            return result;
        }

        static void Remove(MemoryStream buffer, int length)
        {
            long remaining = buffer.Length - length;
            if (remaining <= 0)
            {
                buffer.SetLength(0);
                buffer.Position = 0;
                return;
            }

            var temp = new byte[remaining];
            buffer.Position = length;
            buffer.Read(temp, 0, (int)remaining);
            buffer.SetLength(0);
            buffer.Position = 0;
            buffer.Write(temp, 0, (int)remaining);
            buffer.Position = 0;
        }
    }
}