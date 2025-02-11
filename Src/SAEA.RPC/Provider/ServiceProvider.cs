﻿/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RPC.Provider
*文件名： ServiceProvider
*版本号： v7.0.0.1
*唯一标识：9555d1ce-23a8-4302-b470-7abffdebcdfa
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/16 17:35:38
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/16 17:35:38
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Serialization;
using SAEA.RPC.Common;
using SAEA.RPC.Model;
using SAEA.RPC.Net;
using SAEA.Sockets.Interface;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SAEA.RPC.Provider
{
    /// <summary>
    /// RPC服务提供者 
    /// </summary>
    public class ServiceProvider
    {
        Type[] _serviceTypes;

        int _port = 39654;

        bool _started = false;

        RServer _rServer;

        NoticeCollection _noticeCollection = null;

        public delegate void OnErrHander(Exception ex);

        public event OnErrHander OnErr;

        /// <summary>
        /// RPC服务提供者
        /// </summary>
        /// <param name="port"></param>
        /// <param name="bufferSize"></param>
        /// <param name="count"></param>
        public ServiceProvider(int port = 39654, int bufferSize = 10 * 1024, int count = 10000) : this(null, port, bufferSize, count)
        {

        }


        /// <summary>
        /// RPC服务提供者
        /// </summary>
        /// <param name="serviceTypes">null 注册全部rpc服务</param>
        /// <param name="port"></param>
        /// <param name="bufferSize"></param>
        /// <param name="count"></param>
        public ServiceProvider(Type[] serviceTypes, int port = 39654, int bufferSize = 10 * 1024, int count = 10000)
        {
            _serviceTypes = serviceTypes;
            _port = port;

            _noticeCollection = new NoticeCollection();

            _rServer = new RServer(_port, bufferSize, count);
            _rServer.OnMsg += _RServer_OnMsgAsync;
            _rServer.OnError += _RServer_OnError;

            ExceptionCollector.OnErr += ExceptionCollector_OnErr;
        }

        private void ExceptionCollector_OnErr(string name, Exception ex)
        {
            OnErr(ex);
        }

        private void _RServer_OnError(string ID, Exception ex)
        {
            ExceptionCollector.Add("Provider", ex);
        }

        private void _RServer_OnMsgAsync(IUserToken userToken, RSocketMsg msg)
        {
            try
            {
                switch ((RSocketMsgType)msg.Type)
                {
                    case RSocketMsgType.Ping:
                        _rServer.Reply(userToken, new RSocketMsg(RSocketMsgType.Pong) { SequenceNumber = msg.SequenceNumber });
                        break;
                    case RSocketMsgType.Pong:

                        break;
                    case RSocketMsgType.Request:

                        var data = RPCReversal.Reversal(userToken, msg);

                        var rSocketMsg = new RSocketMsg(RSocketMsgType.Response, null, null, data) { SequenceNumber = msg.SequenceNumber };

                        _rServer.Reply(userToken, rSocketMsg);
                        break;
                    case RSocketMsgType.RegistNotice:
                        _noticeCollection.Set(userToken).GetAwaiter();
                        break;
                    case RSocketMsgType.Response:

                        break;
                    case RSocketMsgType.Error:

                        break;
                    case RSocketMsgType.Close:
                        _rServer.Disconnect(userToken);
                        break;
                }
            }
            catch (Exception ex)
            {
                ExceptionCollector.Add("Provider", ex);
            }
        }

        /// <summary>
        /// 启动服务
        /// </summary>
        public void Start()
        {
            if (!_started)
            {
                _rServer.Start();

                RPCMapping.Regists(_serviceTypes);

                _started = true;
            }
        }
        /// <summary>
        /// 停止服务
        /// </summary>
        public void Stop()
        {
            if (_started)
            {
                _rServer.Stop();
                _started = false;
            }
        }

        /// <summary>
        /// 向注册了接收通知的rpc client 发送通知
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="t"></param>
        public async Task Notice<T>(T t)
        {
            if (t != null)
            {
                var list = await _noticeCollection.GetListAsync();

                if (list != null && list.Any())
                {
                    var data = SAEASerialize.Serialize(t);

                    var msg = new RSocketMsg(RSocketMsgType.Notice, null, null, data) { SequenceNumber = UniqueKeyHelper.Next() };

                    foreach (var item in list)
                    {
                        try
                        {
                            _rServer.Reply(item, msg);
                        }
                        catch (Exception ex)
                        {
                            LogHelper.Warn("ServiceProvider.Notice 出现异常", ex);
                        }
                    }
                }
            }
        }

    }
}
