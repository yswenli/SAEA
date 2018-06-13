/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RPC.Provider
*文件名： ServiceProvider
*版本号： V1.0.0.0
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
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.Commom;
using SAEA.RPC.Common;
using SAEA.RPC.Model;
using SAEA.RPC.Net;
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
        }

        private void _RServer_OnError(string ID, Exception ex)
        {
            throw new RPCSocketException("RPCProvider Socket Excetion", ex);
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
                    _RServer.Reply(userToken, new RSocketMsg(RSocketMsgType.Response, null, null, 
                        RPCReversal.Reversal(userToken, msg)) { SequenceNumber = msg.SequenceNumber });
                    //ConsoleHelper.WriteLine($"3 provider send: {msg.SequenceNumber}");
                    break;
                case RSocketMsgType.Response:

                    break;
                case RSocketMsgType.RequestBig:

                    break;
                case RSocketMsgType.ResponseBig:

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
