/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets
*文件名： Class1
*版本号： V1.0.0.0
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
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace SAEA.Sockets.Core
{
    /// <summary>
    /// 上下文制作工厂
    /// 根据传入的Context类型来实现具体的连接信息类或数据接收解析方式
    /// </summary>
    internal sealed class ContextFactory
    {
        Type _userTokenType;

        Type _coderType;

        bool _cache = true;

        int _count = 10000;

        int _bufferSize = 100 * 1024;

        object _locker = new object();

        ConcurrentQueue<IUserToken> _utQueue = new ConcurrentQueue<IUserToken>();

        public ContextFactory(IContext context, int bufferSize = 100 * 1024, bool cache = true, int count = 10000)
        {
            _bufferSize = bufferSize;

            _cache = cache;

            _count = count;

            _userTokenType = context.UserToken.GetType();

            _coderType = context.UserToken.Coder.GetType();

            if (cache)
            {
                for (int i = 0; i < _count; i++)
                {
                    _utQueue.Enqueue(InitUserToken());
                }
            }
        }

        private IUserToken InitUserToken()
        {
            IUserToken userToken = (IUserToken)Activator.CreateInstance(_userTokenType);
            ICoder coder = (ICoder)Activator.CreateInstance(_coderType);
            userToken.Coder = coder;
            userToken.Buffer = new byte[_bufferSize];
            return userToken;
        }



        public IUserToken GetUserToken(Socket socket)
        {
            IUserToken userToken = null;

            if (_cache)
            {
                if (!_utQueue.TryDequeue(out userToken))
                {
                    userToken = this.InitUserToken();
                }
            }
            else
            {
                userToken = this.InitUserToken();
            }
            userToken.Socket = socket;
            userToken.ID = socket.RemoteEndPoint.ToString();
            userToken.Linked = DateTimeHelper.Now;
            userToken.Actived = DateTimeHelper.Now;
            return userToken;
        }

        public void Free(IUserToken userToken)
        {
            try
            {
                if (userToken.Socket != null && userToken.Socket.Connected)
                {
                    userToken.Socket.Close();
                }
                if (_cache)
                {
                    Array.Clear(userToken.Buffer, 0, userToken.Buffer.Length);
                    _utQueue.Enqueue(userToken);
                }
                else
                {
                    userToken?.Dispose();
                }
            }
            catch { }
        }

        public void Clear()
        {
            if (_cache)
            {
                _utQueue.Clear();
            }
        }

    }
}
