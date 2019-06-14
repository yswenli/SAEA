/****************************************************************************
*项目名称：SAEA.WebSocket.Core
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.WebSocket.Core
*类 名 称：WSServerImpl
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/13 15:37:28
*描述：
*=====================================================================
*修改时间：2019/6/13 15:37:28
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.Sockets.Core;
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Interface;
using SAEA.WebSocket.Model;
using SAEA.WebSocket.Type;
using System;
using System.Threading;

namespace SAEA.WebSocket.Core
{
    /// <summary>
    /// websocket server,
    /// iocp实现
    /// </summary>
    internal class WSServerImpl : IocpServerSocket, IWSServer
    {
        int _heartSpan = 20 * 1000;

        public WSServerImpl(int port = 39654, int heartSpan = 20 * 1000, int bufferSize = 1024, int count = 60000) : base(new WSContext(), bufferSize, count, port: port)
        {
            _heartSpan = heartSpan;

            this.HeartAsync();
        }

        public event Action<string, WSProtocal> OnMessage;


        protected override void OnReceiveBytes(IUserToken userToken, byte[] data)
        {
            var ut = (WSUserToken)(userToken);

            if (!ut.IsHandSharked)
            {
                byte[] resData = null;

                var result = ut.GetReplayHandShake(data, out resData);

                if (result)
                {
                    base.BeginSend(ut, resData);
                    ut.IsHandSharked = true;
                }
            }
            else
            {
                var coder = (WSCoder)ut.Unpacker;
                coder.Unpack(data, (d) =>
                {
                    var wsProtocal = (WSProtocal)d;
                    switch (wsProtocal.Type)
                    {
                        case (byte)WSProtocalType.Close:
                            ReplyClose(ut, wsProtocal);
                            break;
                        case (byte)WSProtocalType.Ping:
                            ReplyPong(ut, wsProtocal);
                            break;
                        case (byte)WSProtocalType.Binary:
                        case (byte)WSProtocalType.Text:
                        case (byte)WSProtocalType.Cont:
                            OnMessage?.Invoke(ut.ID, (WSProtocal)d);
                            break;
                        case (byte)WSProtocalType.Pong:
                            break;
                        default:
                            var error = string.Format("收到未定义的Opcode={0}", d.Type);
                            break;
                    }

                }, (h) => { }, null);
            }
        }


        private void ReplyBase(WSUserToken ut, WSProtocalType type, byte[] content)
        {
            var byts = new WSProtocal(type, content).ToBytes();

            base.SendAsync(ut.ID, byts);
        }

        private void ReplyBase(WSUserToken ut, WSProtocal data)
        {
            var byts = data.ToBytes();

            base.SendAsync(ut.ID, byts);
        }

        private void ReplyPong(WSUserToken ut, WSProtocal data)
        {
            ReplyBase(ut, WSProtocalType.Pong, data.Content);
        }

        private void ReplyClose(WSUserToken ut, WSProtocal data)
        {
            ReplyBase(ut, WSProtocalType.Close, data.Content);
        }


        /// <summary>
        /// 回复客户端消息
        /// </summary>
        /// <param name="id"></param>
        /// <param name="data"></param>
        public void Reply(string id, WSProtocal data)
        {
            var ut = SessionManager.Get(id);
            ReplyBase((WSUserToken)ut, data);
        }

        /// <summary>
        /// 发送关闭
        /// </summary>
        /// <param name="id"></param>
        /// <param name="data"></param>
        public void Disconnect(string id, WSProtocal data)
        {
            var ut = SessionManager.Get(id);
            ReplyBase((WSUserToken)ut, data);

        }

        /// <summary>
        /// 反 ping
        /// </summary>
        private void HeartAsync()
        {
            ThreadHelper.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        Thread.Sleep(_heartSpan);

                        var list = SessionManager.ToList();

                        if (list != null && list.Count > 0)

                            foreach (WSUserToken item in list)
                            {
                                if (item.Actived.AddMilliseconds(_heartSpan) < DateTimeHelper.Now)
                                    ReplyBase(item, WSProtocalType.Ping, null);
                            }
                    }
                    catch { }
                }
            }, true, ThreadPriority.Highest);
        }



    }

}
