/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RPC.Consumer
*文件名： ServiceConsumer
*版本号： V1.0.0.0
*唯一标识：aca3e22d-c8b2-402b-b1fd-77882c780532
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/17 15:14:36
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/17 15:14:36
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.Commom;
using SAEA.RPC.Common;
using SAEA.RPC.Model;
using SAEA.RPC.Net;
using SAEA.RPC.Serialize;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.RPC.Consumer
{
    /// <summary>
    /// rpc消费者
    /// </summary>
    public class ServiceConsumer
    {
        RClient _rClient = null;

        SyncHelper<byte[]> _syncHelper = new SyncHelper<byte[]>();

        bool _isConnected = false;

        DateTime _actived = DateTimeHelper.Now;

        public ServiceConsumer() : this(new Uri("rpc://127.0.0.1:39654")) { }

        public ServiceConsumer(Uri uri)
        {
            _rClient = new RClient(uri);
            _rClient.OnError += _rClient_OnError;
            _rClient.OnMsg += _rClient_OnMsg;

            AutoResetEvent autoResetEvent = new AutoResetEvent(false);

            _rClient.ConnectAsync((s) =>
            {
                if (s == System.Net.Sockets.SocketError.Success)
                {
                    _isConnected = true;
                }
                else throw new RPCSocketException("ServiceConsumer.ConnectAsync 连接失败！");
                autoResetEvent.Set();
            });
            autoResetEvent.WaitOne();
            autoResetEvent.Close();
            KeepAlive();
        }

        private void _rClient_OnMsg(RSocketMsg msg)
        {
            switch ((RSocketMsgType)msg.Type)
            {
                case RSocketMsgType.Ping:
                    break;
                case RSocketMsgType.Pong:

                    break;
                case RSocketMsgType.Request:
                    break;
                case RSocketMsgType.Response:
                    _syncHelper.Set(msg.SequenceNumber, msg.Data);
                    break;
                case RSocketMsgType.RequestBig:
                    break;
                case RSocketMsgType.ResponseBig:
                    break;
                case RSocketMsgType.Close:
                    break;
            }
        }

        private void _rClient_OnError(string ID, Exception ex)
        {
            throw new RPCSocketException("ServiceConsumer Socket Exception:" + ex.Message, ex);
        }


        private void BaseSend(RSocketMsg msg)
        {
            _rClient.Send(msg);

            _actived = DateTimeHelper.Now;
        }

        /// <summary>
        /// 保持连接
        /// </summary>
        private void KeepAlive()
        {
            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    if (_isConnected)
                    {
                        if (_actived.AddMinutes(1) < DateTimeHelper.Now)
                        {
                            BaseSend(new RSocketMsg(RSocketMsgType.Ping));
                        }
                        ThreadHelper.Sleep(5 * 1000);
                    }
                    else
                    {
                        ThreadHelper.Sleep(10);
                    }
                }
            });
        }

        /// <summary>
        /// 本地rpc远程代理
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="method"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public byte[] RemoteCall(string serviceName, string method, byte[] args)
        {
            if (!_isConnected) throw new RPCSocketException("连接到服务器失败");

            byte[] result = null;

            var msg = new RSocketMsg(RSocketMsgType.Request, serviceName, method)
            {
                SequenceNumber = UniqueKeyHelper.Next()
            };

            msg.Data = args;

            BaseSend(msg);

            if (_syncHelper.WaitOne(msg.SequenceNumber, (r) =>
            {
                result = r;
            }, 10 * 1000))
            {
                return result;
            }
            return null;
        }

        /// <summary>
        /// 调用远程RPC
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serviceName"></param>
        /// <param name="method"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public T RemoteCall<T>(string serviceName, string method, params object[] args)
        {
            T t = default(T);

            byte[] abytes = null;

            if (args != null)
            {
                abytes = ParamsSerializeUtil.Serialize(args);
            }
            var data = RemoteCall(serviceName, method, abytes);
            if (data != null)
            {
                int offset = 0;
                t = (T)ParamsSerializeUtil.Deserialize(typeof(T), data, ref offset);
            }
            return t;
        }
    }
}
