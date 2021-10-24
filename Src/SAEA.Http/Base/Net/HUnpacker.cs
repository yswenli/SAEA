/****************************************************************************
*Copyright (c) 2018-2021yswenli All Rights Reserved.
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
    class HUnpacker : IUnpacker
    {
        List<byte> _cache = new List<byte>();

        int _totlalLen = -1;

        public void Unpack(byte[] data, Action<ISocketProtocal> unpackCallback, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
        {

        }


        /// <summary>
        /// GetRequest
        /// </summary>
        /// <param name="id"></param>
        /// <param name="data"></param>
        /// <param name="onUnpackage"></param>
        public void GetRequest(string id, byte[] data, Action<HttpMessage> onUnpackage)
        {
            _cache.AddRange(data);

            var buffer = _cache.ToArray();

            if (_totlalLen == -1)
            {
                if (RequestDataReader.Analysis(buffer, out HttpMessage httpMessage))
                {
                    httpMessage.ID = id;

                    var contentLen = httpMessage.ContentLength;

                    var positon = httpMessage.Position;

                    _totlalLen = contentLen + positon;

                    //post需要处理body
                    if (httpMessage.Method == ConstHelper.GET)
                    {
                        _cache.RemoveRange(0, _totlalLen);
                        _totlalLen = -1;
                        onUnpackage.Invoke(httpMessage);
                    }
                    else
                    {
                        if (buffer.Length >= _totlalLen)
                        {
                            RequestDataReader.AnalysisBody(buffer, httpMessage);
                            _cache.RemoveRange(0, _totlalLen);
                            _totlalLen = -1;
                            onUnpackage.Invoke(httpMessage);
                        }
                        else
                        {
                            return;
                        }
                    }
                }
            }
            else if (buffer.Length >= _totlalLen)
            {
                if (RequestDataReader.Analysis(buffer, out HttpMessage httpMessage1))
                {
                    httpMessage1.ID = id;
                    RequestDataReader.AnalysisBody(buffer, httpMessage1);
                    _cache.RemoveRange(0, _totlalLen);
                    _totlalLen = -1;
                    onUnpackage.Invoke(httpMessage1);
                }
                else
                {
                    throw new DataMisalignedException("解析失败");
                }
            }
            else
            {
                return;
            }
        }

        /// <summary>
        /// Clear
        /// </summary>
        public void Clear()
        {
            _totlalLen = -1;
            _cache.Clear();
        }
    }
}
