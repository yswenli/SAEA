/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Commom
*文件名： GZipHelper
*版本号： v4.3.1.2
*唯一标识：0957f3bb-7462-4ff0-867d-0a8c9411f2eb
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/12 9:33:39
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/12 9:33:39
*修改人： yswenli
*版本号： v4.3.1.2
*描述：
*
*****************************************************************************/
using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace SAEA.Common
{
    /// <summary>
    /// gzip压缩
    /// </summary>
    public class GZipHelper
    {
        /// <summary>
        ///     压缩
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static byte[] Compress(byte[] data)
        {
            if (data == null) return null;

            byte[] buffer = null;
            using (MemoryStream stream = new MemoryStream())
            {
                using (GZipStream inflateStream = new GZipStream(stream, CompressionMode.Compress, true))
                {
                    inflateStream.Write(data, 0, data.Length);
                }
                stream.Seek(0, SeekOrigin.Begin);
                int length = Convert.ToInt32(stream.Length);
                buffer = new byte[length];
                stream.Read(buffer, 0, length);
            }
            return buffer;
        }

        /// <summary>
        ///     解压缩
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static byte[] Decompress(byte[] data)
        {
            if (data == null) return null;

            using (var srcMs = new MemoryStream(data))
            {
                using (var zipStream = new GZipStream(srcMs, CompressionMode.Decompress))
                {
                    using (var ms = new MemoryStream())
                    {
                        var bytes = new byte[40960];
                        int n;
                        while ((n = zipStream.Read(bytes, 0, bytes.Length)) > 0)
                            ms.Write(bytes, 0, n);
                        return ms.ToArray();
                    }
                }
            }
        }

        /// <summary>
        ///     压缩
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string Compress(string data)
        {
            return Encoding.UTF8.GetString(Compress(Encoding.UTF8.GetBytes(data)));
        }

        /// <summary>
        ///     解压缩
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string Decompress(string data)
        {
            return Encoding.UTF8.GetString(Decompress(Encoding.UTF8.GetBytes(data)));
        }

        /// <summary>
        /// Deflate
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static byte[] Deflate(byte[] data)
        {
            if (data == null) return null;

            byte[] buffer = null;
            using (MemoryStream stream = new MemoryStream())
            {
                using (DeflateStream inflateStream = new DeflateStream(stream, CompressionMode.Compress, true))
                {
                    inflateStream.Write(data, 0, data.Length);
                }

                stream.Seek(0, SeekOrigin.Begin);

                int length = Convert.ToInt32(stream.Length);
                buffer = new byte[length];
                stream.Read(buffer, 0, length);
            }

            return buffer;
        }

        public static byte[] UnDeflate(byte[] compressedData)
        {
            if (compressedData == null) return null;

            int deflen = compressedData.Length * 2;
            byte[] buffer = null;

            using (MemoryStream stream = new MemoryStream(compressedData))
            {
                using (DeflateStream inflatestream = new DeflateStream(stream, CompressionMode.Decompress))
                {
                    using (MemoryStream uncompressedstream = new MemoryStream())
                    {
                        using (BinaryWriter writer = new BinaryWriter(uncompressedstream))
                        {
                            int offset = 0;
                            while (true)
                            {
                                byte[] tempbuffer = new byte[deflen];

                                int bytesread = inflatestream.Read(tempbuffer, offset, deflen);

                                writer.Write(tempbuffer, 0, bytesread);

                                if (bytesread < deflen || bytesread == 0) break;
                            }
                            uncompressedstream.Seek(0, SeekOrigin.Begin);
                            buffer = uncompressedstream.ToArray();
                        }
                    }
                }
            }
            return buffer;
        }
    }
}
