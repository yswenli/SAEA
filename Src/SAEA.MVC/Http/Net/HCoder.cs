/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.WebAPI.Http.Net
*文件名： HCoder
*版本号： V1.0.0.0
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
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.Sockets.Interface;
using SAEA.WebAPI.Http.Base;
using System;
using System.Collections.Generic;

namespace SAEA.WebAPI.Http.Net
{
    class HCoder : ICoder
    {
        List<byte> _cache = new List<byte>();

        object _locker = new object();

        bool _isAnalysis = false;


        public void Pack(byte[] data, Action<DateTime> onHeart, Action<ISocketProtocal> onUnPackage, Action<byte[]> onFile)
        {

        }

        /// <summary>
        /// 解析http请求的数据
        /// </summary>
        /// <param name="data"></param>
        /// <param name="onUnpackage"></param>
        public void GetRequest(byte[] data, Action<RequestDataReader> onUnpackage)
        {
            lock (_locker)
            {
                _cache.AddRange(data);

                var buffer = _cache.ToArray();

                var requestDataReader = new RequestDataReader();

                if (!_isAnalysis)
                {
                    _isAnalysis = requestDataReader.Analysis(buffer);
                }
                if (_isAnalysis)
                {
                    //post需要处理body
                    if (requestDataReader.Method == ConstString.POSTStr)
                    {
                        var contentLen = requestDataReader.ContentLength;
                        var positon = requestDataReader.Position;
                        var totlalLen = contentLen + positon;
                        if (buffer.Length == totlalLen)
                        {
                            requestDataReader.AnalysisBody(buffer);
                            onUnpackage.Invoke(requestDataReader);
                            Array.Clear(buffer, 0, buffer.Length);
                            _cache.Clear();
                        }
                    }
                    else
                    {
                        onUnpackage.Invoke(requestDataReader);
                        Array.Clear(buffer, 0, buffer.Length);
                        _cache.Clear();
                    }
                }
            }
        }


        public void Dispose()
        {
            _cache.Clear();
        }
    }
}
