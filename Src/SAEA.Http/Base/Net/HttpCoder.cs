/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http.Base.Net
*文件名： HCoder
*版本号： v7.0.0.1
*唯一标识：1c78283d-e311-4d8d-b781-395253c9454c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/8 17:19:30
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/8 17:19:30
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;

using SAEA.Common;
using SAEA.Sockets.Interface;

namespace SAEA.Http.Base.Net
{
    /// <summary>
    /// HttpCoder类实现了ICoder接口，用于处理HTTP协议的编码和解码
    /// </summary>
    class HttpCoder : ICoder
    {
        // 缓存接收到的数据
        List<byte> _cache = new List<byte>();

        /// <summary>
        /// 将ISocketProtocal对象编码为字节数组
        /// </summary>
        /// <param name="protocal">ISocketProtocal对象</param>
        /// <returns>编码后的字节数组</returns>
        public byte[] Encode(ISocketProtocal protocal)
        {
            return protocal.ToBytes();
        }

        /// <summary>
        /// 将字节数组解码为ISocketProtocal对象列表
        /// </summary>
        /// <param name="data">待解码的字节数组</param>
        /// <param name="onHeart">心跳包处理回调</param>
        /// <param name="onFile">文件包处理回调</param>
        /// <returns>解码后的ISocketProtocal对象列表</returns>
        public List<ISocketProtocal> Decode(byte[] data, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 解析HTTP请求
        /// </summary>
        /// <param name="id">请求ID</param>
        /// <param name="data">请求数据</param>
        /// <returns>解析后的HttpMessage列表</returns>
        public List<HttpMessage> GetRequest(string id, byte[] data)
        {
            var result = new List<HttpMessage>();
            _cache.AddRange(data);
            byte[] buffer;

            while (_cache.Count > 0)
            {
                buffer = _cache.ToArray();
                try
                {
                    if (RequestDataReader.Analysis(buffer, out HttpMessage httpMessage) > 0)
                    {
                        httpMessage.ID = id;

                        var contentLen = httpMessage.ContentLength;

                        var positon = httpMessage.Position;

                        var offset = contentLen + positon;

                        if (buffer.Length >= offset)
                        {
                            // GET请求没有body
                            if (httpMessage.Method == ConstHelper.GET)
                            {
                                _cache.RemoveRange(0, offset);
                                result.Add(httpMessage);
                                continue;
                            }
                            else
                            {
                                if (RequestDataReader.AnalysisBody(buffer, httpMessage))
                                {
                                    _cache.RemoveRange(0, offset);
                                    result.Add(httpMessage);
                                    continue;
                                }
                            }
                        }
                        else
                        {
                            return result;
                        }
                    }
                }
                catch
                {
                    return result;
                }
                finally
                {
                    buffer.Clear();
                }
            }
            return result;
        }

        /// <summary>
        /// 清除编码器内部状态
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
        }
    }
}
