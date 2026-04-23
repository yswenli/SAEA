/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Common.Encryption
*文件名： GZipHelper
*版本号： v26.4.23.1
*唯一标识：ec8f2fe8-fe67-4096-8f1c-47de1f300f4e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/16 17:50:24
*描述：GZipHelper接口
*
*=====================================================================
*修改标记
*修改时间：2020/12/16 17:50:24
*修改人： yswenli
*版本号： v26.4.23.1
*描述：GZipHelper接口
*
*****************************************************************************/
using SAEA.Common.Caching;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace SAEA.Common.Encryption
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
                        const int bufferSize = 40960;
                        byte[] bytes = null;
                        try
                        {
                            bytes = MemoryPoolManager.Rent(bufferSize);
                            int n;
                            while ((n = zipStream.Read(bytes, 0, bufferSize)) > 0)
                                ms.Write(bytes, 0, n);
                            return ms.ToArray();
                        }
                        finally
                        {
                            if (bytes != null)
                            {
                                MemoryPoolManager.Return(bytes, bufferSize);
                            }
                        }
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
            byte[] tempbuffer = null;

            using (MemoryStream stream = new MemoryStream(compressedData))
            {
                using (DeflateStream inflatestream = new DeflateStream(stream, CompressionMode.Decompress))
                {
                    using (MemoryStream uncompressedstream = new MemoryStream())
                    {
                        using (BinaryWriter writer = new BinaryWriter(uncompressedstream))
                        {
                            int offset = 0;
                            try
                            {
                                tempbuffer = MemoryPoolManager.Rent(deflen);
                                while (true)
                                {
                                    int bytesread = inflatestream.Read(tempbuffer, offset, deflen);

                                    writer.Write(tempbuffer, 0, bytesread);

                                    if (bytesread < deflen || bytesread == 0) break;
                                }
                                uncompressedstream.Seek(0, SeekOrigin.Begin);
                                buffer = uncompressedstream.ToArray();
                            }
                            finally
                            {
                                if (tempbuffer != null)
                                {
                                    MemoryPoolManager.Return(tempbuffer, deflen);
                                }
                            }
                        }
                    }
                }
            }
            return buffer;
        }
    }
}