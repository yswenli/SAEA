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
*命名空间：SAEA.Http.Base.Net
*文件名： HttpCoder
*版本号： v26.4.23.1
*唯一标识：f4c6e124-5373-4492-9b9b-f5c59bb1694c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2025/02/10 17:07:21
*描述：HttpCoder编解码类
*
*=====================================================================
*修改标记
*修改时间：2025/02/10 17:07:21
*修改人： yswenli
*版本号： v26.4.23.1
*描述：HttpCoder编解码类
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
                    var analysisResult = RequestDataReader.Analysis(buffer, out HttpMessage httpMessage);
                    if (analysisResult > 0)
                    {
                        httpMessage.ID = id;

                        var contentLen = httpMessage.ContentLength;

                        var positon = httpMessage.Position;

                        var offset = contentLen + positon;

                        // GET请求
                        if (httpMessage.Method == ConstHelper.GET)
                        {
                            // 对于GET请求，如果有Content-Length，需要移除整个请求的长度
                            if (_cache.Count >= offset)
                            {
                                _cache.RemoveRange(0, offset);
                            }
                            else
                            {
                                // 数据不完整，等待更多数据
                                break;
                            }
                            
                            result.Add(httpMessage);
                            continue;
                        }
                        else
                        {
                            if (buffer.Length >= offset)
                            {
                                if (RequestDataReader.AnalysisBody(buffer, httpMessage))
                                {
                                    _cache.RemoveRange(0, offset);
                                    result.Add(httpMessage);
                                    continue;
                                }
                                else
                                {
                                    // 无法解析请求体，返回已解析的结果，保留缓存中的数据
                                    return result;
                                }
                            }
                            else
                            {
                                // 数据不完整，等待更多数据
                                return result;
                            }
                        }
                    }
                    else
                    {
                        // 无法解析请求头，可能是数据不完整，等待更多数据
                        return result;
                    }
                }
                catch
                {
                    // 解析异常，尝试移除一个字节，然后继续解析
                    _cache.RemoveAt(0);
                    continue;
                }
                finally
                {
                    // 字节数组不需要手动清除，GC会自动处理
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