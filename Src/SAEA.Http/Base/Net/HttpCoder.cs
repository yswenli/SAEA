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

        Action<byte[]> _rawOnData;

        Action<Exception> _rawOnError;

        /// <summary>
        /// 是否已切换为 WebSocket 原始字节模式
        /// </summary>
        public bool IsRaw { get; private set; }

        /// <summary>
        /// 将ISocketProtocal对象编码为字节数组
        /// </summary>
        /// <param name="protocal">ISocketProtocal对象</param>
        /// <returns>编码后的字节数组</returns>
        public void Encode(ISocketProtocal protocal, System.Buffers.IBufferWriter<byte> writer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 将字节数组解码为ISocketProtocal对象列表
        /// </summary>
        /// <param name="data">待解码的字节数组</param>
        /// <param name="onHeart">心跳包处理回调</param>
        /// <param name="onFile">文件包处理回调</param>
        /// <returns>解码后的ISocketProtocal对象列表</returns>
        public SAEA.Sockets.Base.DecodedFrames Decode(System.Buffers.ReadOnlySequence<byte> data, Action<DateTime> onHeart = null, Action<ReadOnlyMemory<byte>> onFile = null)
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

            // 已升级为 WebSocket：后续字节不再按 HTTP 解析
            if (IsRaw)
            {
                DispatchRaw(data);
                return result;
            }

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

                            // 升级请求之后可能紧跟客户端首帧，不能继续消费后面的字节
                            if (IsWebSocketUpgrade(httpMessage)) return result;

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

                                    if (IsWebSocketUpgrade(httpMessage)) return result;

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
                    // 本轮已经解析出请求时，说明剩余字节可能不是 HTTP（例如升级后的首帧），保留缓存
                    if (result.Count > 0) return result;

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
        /// 切换到 WebSocket 原始字节模式，并把已缓存但未解析的字节立即交给 onData。
        /// 101 响应与客户端首帧可能落在同一个 TCP 段里，必须 drain，否则会丢首帧。
        /// </summary>
        /// <param name="onData">后续收到的原始字节</param>
        /// <param name="onError">onData 抛出的异常</param>
        public void UpgradeRaw(Action<byte[]> onData, Action<Exception> onError)
        {
            _rawOnData = onData;
            _rawOnError = onError;
            IsRaw = true;

            if (_cache.Count > 0)
            {
                var rest = _cache.ToArray();
                _cache.Clear();
                DispatchRaw(rest);
            }
        }

        void DispatchRaw(byte[] data)
        {
            if (data == null || data.Length == 0) return;

            try
            {
                _rawOnData?.Invoke(data);
            }
            catch (Exception ex)
            {
                try
                {
                    _rawOnError?.Invoke(ex);
                }
                catch
                {
                }
            }
        }

        static bool IsWebSocketUpgrade(HttpMessage httpMessage)
        {
            var headers = httpMessage?.Headers;
            if (headers == null) return false;
            if (!headers.TryGetValue("Upgrade", out var upgrade) || string.IsNullOrEmpty(upgrade)) return false;
            return upgrade.IndexOf("websocket", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 清除编码器内部状态
        /// </summary>
        public void Clear()
        {
            _cache.Clear();

            // token 会被复用：不复位 raw 标记的话，后续 HTTP 请求会被当成 WS 帧
            IsRaw = false;
            _rawOnData = null;
            _rawOnError = null;
        }
    }
}