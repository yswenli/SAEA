/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RPC.Provider
*文件名： ServiceProvider
*版本号： V3.5.9.1
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
*版本号： V3.5.9.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.RPC.Common;
using SAEA.RPC.Model;
using SAEA.RPC.Net;
using SAEA.RPC.Serialize;
using System;

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

        RServer _RServer;

        public delegate void OnErrHander(Exception ex);

        public event OnErrHander OnErr;

        /// <summary>
        /// RPC服务提供者
        /// </summary>
        /// <param name="port"></param>
        public ServiceProvider(int port = 39654) : this(null, port)
        {

        }

        /// <summary>
        /// RPC服务提供者
        /// </summary>
        /// <param name="serviceTypes">null 自动注册全部服务</param>
        /// <param name="port"></param>
        public ServiceProvider(Type[] serviceTypes, int port = 39654)
        {
            _serviceTypes = serviceTypes;
            _port = 39654;

            _RServer = new RServer();
            _RServer.OnMsg += _RServer_OnMsg;
            _RServer.OnError += _RServer_OnError;

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

        private void _RServer_OnMsg(Sockets.Interface.IUserToken userToken, RSocketMsg msg)
        {
            //ConsoleHelper.WriteLine($"2 provider receive: {msg.SequenceNumber}");

            switch ((RSocketMsgType)msg.Type)
            {
                case RSocketMsgType.Ping:
                    _RServer.Reply(userToken, new RSocketMsg(RSocketMsgType.Pong) { SequenceNumber = msg.SequenceNumber });
                    break;
                case RSocketMsgType.Pong:

                    break;
                case RSocketMsgType.Request:
                    try
                    {
                        var data = RPCReversal.Reversal(userToken, msg);

                        var rSocketMsg = new RSocketMsg(RSocketMsgType.Response, null, null, data) { SequenceNumber = msg.SequenceNumber };

                        _RServer.Reply(userToken, rSocketMsg);
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            ExceptionCollector.Add("Provider", ex);

                            if (userToken.Socket != null && userToken.Socket.Connected)
                            {
                                var eData = ParamsSerializeUtil.Serialize(ex.Message);
                                var eMsg = new RSocketMsg(RSocketMsgType.Error, null, null, eData) { SequenceNumber = msg.SequenceNumber };
                                _RServer.Reply(userToken, eMsg);
                            }
                        }
                        catch (Exception sex)
                        {
                            Console.WriteLine($"_RServer_OnMsg.Error:{sex.Message}");
                        }
                    }
                    //ConsoleHelper.WriteLine($"3 provider send: {msg.SequenceNumber}");
                    break;
                case RSocketMsgType.Response:

                    break;
                case RSocketMsgType.Error:

                    break;
                case RSocketMsgType.Close:
                    _RServer.Disconnect(userToken);
                    break;
            }
        }

        /// <summary>
        /// 启动服务
        /// </summary>
        public void Start()
        {
            if (!_started)
            {
                _RServer.Start(_port);

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
                _started = false;
                _RServer.Stop();
            }
        }

    }
}
