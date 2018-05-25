/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RPC.Net
*文件名： RClient
*版本号： V1.0.0.0
*唯一标识：6921ced2-8a62-45a7-89c6-84d1301c1a28
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/16 16:16:42
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/16 16:16:42
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.RPC.Model;
using SAEA.Sockets.Core;
using System;
using System.Threading;

namespace SAEA.RPC.Net
{
    internal class RClient : BaseClientSocket
    {
        #region event

        /// <summary>
        /// 收到消息
        /// </summary>
        public event Action<RSocketMsg> OnMsg;

        #endregion

        public RClient(Uri uri) : this(100 * 1024, uri.Host, uri.Port)
        {
            if (string.IsNullOrEmpty(uri.Scheme) || uri.Scheme.ToLower() != "rpc")
            {
                throw new RPCSocketException("当前连接协议不正确，请使用格式rpc://ip:port");
            }
        }

        public RClient(int bufferSize = 100 * 1024, string ip = "127.0.0.1", int port = 39654) : base(new RContext(), ip, port, bufferSize)
        {

        }


        public bool Connect()
        {
            AutoResetEvent autoRestEvent = new AutoResetEvent(false);

            base.ConnectAsync((s) =>
            {
                if (s == System.Net.Sockets.SocketError.Success)
                {
                    autoRestEvent.Set();
                }
            });

            return autoRestEvent.WaitOne(10 * 1000);
        }

        protected override void OnReceived(byte[] data)
        {
            ((RCoder)UserToken.Coder).Unpack(data, (r) =>
            {
                OnMsg?.Invoke(r);
            });
        }

        internal void Send(RSocketMsg msg)
        {
            var data = ((RCoder)UserToken.Coder).Encode(msg);
            SendAsync(data);
        }
    }
}
