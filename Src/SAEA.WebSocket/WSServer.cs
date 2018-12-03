/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.WebSocket
*文件名： Class1
*版本号： V3.3.3.5
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： V3.3.3.5
*描述：
*
*****************************************************************************/

using SAEA.Sockets.Core;
using SAEA.Sockets.Interface;
using SAEA.WebSocket.Type;
using SAEA.WebSocket.Model;
using System;
using System.Threading;
using SAEA.Common;

namespace SAEA.WebSocket
{
    public class WSServer : BaseServerSocket
    {
        int _heartSpan = 20 * 1000;

        public WSServer(int heartSpan = 20 * 1000, int bufferSize = 1024, int count = 60000) : base(new WSContext(), bufferSize, count)
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
        /// <param name="ID"></param>
        /// <param name="data"></param>
        public void Reply(string ID, WSProtocal data)
        {
            var ut = SessionManager.Get(ID);
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
