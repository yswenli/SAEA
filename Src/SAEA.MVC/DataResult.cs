/****************************************************************************
*项目名称：SAEA.MVC
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.MVC
*类 名 称：DataResult
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/3/3 11:28:38
*描述：
*=====================================================================
*修改时间：2021/3/3 11:28:38
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Text;

namespace SAEA.MVC
{
    /// <summary>
    /// 数据流
    /// </summary>
    [DataContract]
    public class DataResult : ActionResult
    {
        /// <summary>
        /// 数据流
        /// </summary>
        /// <param name="stream"></param>
        public DataResult(Stream stream)
        {
            List<byte> list = new List<byte>();
            var bytes = new byte[10240];
            int offset = 0;
            int size = 0;
            stream.Position = 0;
            do
            {
                size = stream.Read(bytes, 0, 10240);
                if (size > 0)
                {
                    offset += size;
                    list.AddRange(bytes.AsSpan().Slice(0, size).ToArray());
                }
                else
                {
                    break;
                }
            }
            while (size > 0);

            var result = list.ToArray();
            this.Content = result;
            this.ContentEncoding = Encoding.UTF8;
            this.ContentType = "application/octet-stream";
            this.Status = HttpStatusCode.OK;
            list.Clear();
        }

        /// <summary>
        /// 数据流
        /// </summary>
        /// <param name="data"></param>
        public DataResult(byte[] data) : this(data, Encoding.UTF8)
        {

        }

        /// <summary>
        /// 数据流
        /// </summary>
        /// <param name="data"></param>
        /// <param name="status"></param>
        public DataResult(byte[] data, HttpStatusCode status)
        {
            this.Content = data;
            this.ContentEncoding = Encoding.UTF8;
            this.ContentType = "application/octet-stream";
            this.Status = status;
        }

        /// <summary>
        /// 数据流
        /// </summary>
        /// <param name="data"></param>
        /// <param name="encoding"></param>
        /// <param name="contentType"></param>
        public DataResult(byte[] data, Encoding encoding, string contentType = "application/octet-stream")
        {
            this.Content = data;
            this.ContentEncoding = encoding;
            this.ContentType = contentType;
        }
    }
}
