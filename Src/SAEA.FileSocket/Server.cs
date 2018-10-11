/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.FileSocket
*文件名： Class1
*版本号： V2.2.0.0
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
*版本号： V2.2.0.0
*描述：
*
*****************************************************************************/

using SAEA.Sockets;
using SAEA.Sockets.Core;
using SAEA.Sockets.Model;
using SAEA.Sockets.Handler;
using SAEA.Sockets.Interface;
using System.Threading;
using SAEA.FileSocket.Model;
using SAEA.Common;

namespace SAEA.FileSocket
{
    /// <summary>
    /// 服务器
    /// 采用默认的上下文操作
    /// </summary>
    public class Server : BaseServerSocket
    {
        #region events

        public event OnRequestHandler OnRequested;

        public event OnFileHandler OnFile;

        #endregion

        private long _total;

        private long _in;

        public long Total { get => _total; set => _total = value; }
        public long In { get => _in; set => _in = value; }

        public Server(int bufferSize = 100 * 1024) : base(new Context(), bufferSize, 10)
        {

        }

        public void Allow(string ID)
        {
            var sm = new SocketProtocal()
            {
                BodyLength = 0,
                Type = (byte)SocketProtocalType.AllowReceive
            };

            var data = sm.ToBytes();

            var userToken = SessionManager.Get(ID);
            if (userToken != null)
                SendAsync(userToken, data);
        }

        public void Refuse(string ID)
        {
            var sm = new SocketProtocal()
            {
                BodyLength = 0,
                Type = (byte)SocketProtocalType.RefuseReceive
            };

            var data = sm.ToBytes();

            var userToken = SessionManager.Get(ID);
            if (userToken != null)
                SendAsync(userToken, data);
        }

        /// <summary>
        /// 重写数据接收以实现具体的业务应用场景
        /// 自行实现ICotext、ICoder等可以实现自定义协议
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="data"></param>
        protected override void OnReceiveBytes(IUserToken userToken, byte[] data)
        {
            userToken.Coder.Pack(data, null, (s) =>
            {
                string fileName = string.Empty;

                long length = 0;

                if (s.Content != null)
                {
                    var fi = s.Content.ToInstance<FileMessage>();
                    fileName = fi.FileName;
                    length = fi.Length;
                }

                OnRequested?.Invoke(userToken.ID, fileName, length);

                _total = length;
            }, (f) =>
            {
                Interlocked.Add(ref _in, f.Length);
                OnFile?.Invoke(userToken, f);
            });
        }
    }
}
