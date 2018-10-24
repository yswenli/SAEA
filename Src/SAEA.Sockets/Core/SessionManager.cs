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
*文件名： SessionManager
*版本号： V2.2.2.0
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
*版本号： V2.2.2.0
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;

namespace SAEA.Sockets.Core
{

    /// <summary>
    /// 会话管理器
    /// </summary>
    public class SessionManager
    {
        Type _userTokenType;

        Type _coderType;

        OuterMemoryCacheHelper<IUserToken> _session;

        TimeSpan _timeOut;

        private int _bufferSize = 1024 * 10;

        EventHandler<SocketAsyncEventArgs> _completed = null;

        BufferManager _bufferManager;

        SocketAsyncEventArgsPool _argsPool;

        /// <summary>
        /// 心跳过期事件
        /// </summary>
        public event Action<IUserToken> OnTimeOut;

        /// <summary>
        /// 构造会话管理器
        /// </summary>
        /// <param name="context"></param>
        /// <param name="bufferSize"></param>
        /// <param name="count"></param>
        /// <param name="completed"></param>
        public SessionManager(IContext context, int bufferSize, int count, EventHandler<SocketAsyncEventArgs> completed, TimeSpan timeOut)
        {
            _userTokenType = context.UserToken.GetType();
            _coderType = context.UserToken.Coder.GetType();
            _session = new OuterMemoryCacheHelper<IUserToken>();
            _timeOut = timeOut;
            _bufferSize = bufferSize;
            _completed = completed;

            _bufferManager = new BufferManager(bufferSize * count, bufferSize);
            _bufferManager.InitBuffer();

            _argsPool = new SocketAsyncEventArgsPool(count * 2);
            _argsPool.InitPool(completed);

            //超时处理 timeout handler
            ThreadHelper.PulseAction(() =>
            {
                var values = _session.List.Where(b => b.Expired < DateTimeHelper.Now);
                if (values != null)
                {
                    foreach (var val in values)
                    {
                        if (val != null)
                            OnTimeOut?.Invoke(val.Value);
                    }
                }
            }, new TimeSpan(0, 0, 10), false);
        }

        /// <summary>
        /// 初始化一个IUserToken
        /// </summary>
        /// <returns></returns>
        private IUserToken InitUserToken()
        {
            IUserToken userToken = (IUserToken)Activator.CreateInstance(_userTokenType);
            ICoder coder = (ICoder)Activator.CreateInstance(_coderType);
            userToken.Coder = coder;

            userToken.ReadArgs = _argsPool.Pop();
            _bufferManager.SetBuffer(userToken.ReadArgs);
            userToken.WriteArgs = _argsPool.Pop();
            userToken.ReadArgs.UserToken = userToken.WriteArgs.UserToken = userToken;
            return userToken;
        }

        /// <summary>
        /// 获取usertoken
        /// </summary>
        /// <param name="socket"></param>
        /// <returns></returns>
        public IUserToken GenerateUserToken(Socket socket)
        {
            IUserToken userToken = InitUserToken();
            userToken.Socket = socket;
            userToken.ID = socket.RemoteEndPoint.ToString();
            userToken.Linked = DateTimeHelper.Now;
            userToken.Actived = DateTimeHelper.Now;
            return userToken;
        }


        public void Set(IUserToken IUserToken)
        {
            _session.Set(IUserToken.ID, IUserToken, _timeOut);

        }

        public void Active(string ID)
        {
            _session.Active(ID, _timeOut);
        }

        public IUserToken Get(string ID)
        {
            return _session.Get(ID);
        }

        /// <summary>
        /// 释放IUserToken
        /// </summary>
        /// <param name="userToken"></param>
        public bool Free(IUserToken userToken)
        {
            if (_session.Get(userToken.ID) != null)
            {
                _session.Del(userToken.ID);
                if (userToken.Socket != null)
                {
                    try
                    {
                        if (userToken.Socket.Connected)
                            userToken.Socket.Shutdown(SocketShutdown.Both);
                    }
                    catch { }
                    _bufferManager.FreeBuffer(userToken.ReadArgs);
                    _argsPool.Push(userToken.ReadArgs);
                    _argsPool.Push(userToken.WriteArgs);
                    userToken.Dispose();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 获取全部会话
        /// </summary>
        /// <returns></returns>
        public List<IUserToken> ToList()
        {
            return _session.List.Select(b => b.Value).ToList();
        }

        /// <summary>
        /// 清理全部会话
        /// </summary>
        public void Clear()
        {
            var list = ToList();
            foreach (var item in list)
            {
                item.Dispose();
            }
            _session.Clear();
        }
    }
}
