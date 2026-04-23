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
*命名空间：SAEA.MVC
*文件名： DataResult
*版本号： v26.4.23.1
*唯一标识：4e4e92ee-08ac-4f0e-b988-35030268207e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/03 14:09:29
*描述：
*
*=====================================================================
*修改标记
*修改时间：2021/03/03 14:09:29
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
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
            try
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
            finally
            {
                stream?.Close();
            }
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
