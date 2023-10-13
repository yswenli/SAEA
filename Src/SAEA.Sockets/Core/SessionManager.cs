/****************************************************************************
* 
 ____    _    _____    _      ____             _        _   
/ ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
\___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
 ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
|____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|


*Copyright (c) 2018-2022yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets
*文件名： SessionManager
*版本号： v7.0.0.1
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
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/

using System;
using System.Net;
using System.Net.Sockets;

using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Sockets.Interface;

namespace SAEA.Sockets.Core
{

    /// <summary>
    /// 会话管理器
    /// </summary>
    public class SessionManager
    {
        UserTokenPool _userTokenPool;

        MemoryCache<IUserToken> _sessionCache;

        TimeSpan _freeTime;

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
        /// <param name="freetime"></param>
        public SessionManager(IContext<IUnpacker> context, int bufferSize, int count, EventHandler<SocketAsyncEventArgs> completed, TimeSpan freetime)
        {
            _sessionCache = new MemoryCache<IUserToken>();

            _freeTime = freetime;

            _userTokenPool = new UserTokenPool(context, count, bufferSize, completed);

            _sessionCache.OnChanged += _sessionCache_OnChanged;
        }


        private void _sessionCache_OnChanged(MemoryCache<IUserToken> obj, bool isAdd, IUserToken userToken)
        {
            if (!isAdd)
            {
                OnTimeOut?.Invoke(userToken);
            }
        }

        /// <summary>
        /// TCP获取usertoken
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public IUserToken BindUserToken(Socket socket, int timeOut)
        {
            IUserToken userToken = _userTokenPool.Dequeue(timeOut);
            if (userToken == null) throw new Exception("UserToken池中资源已耗尽");
            userToken.Socket = socket;
            userToken.ID = socket.RemoteEndPoint.ToString();
            userToken.Actived = userToken.Linked = DateTimeHelper.Now;
            _sessionCache.Set(userToken.ID, userToken, _freeTime);
            return userToken;
        }

        #region UDP
        /// <summary>
        /// UDP获取usertoken
        /// 如果IUserToken数量耗尽时会出现死锁
        /// </summary>
        /// <param name="socket"></param>
        /// <returns></returns>
        public IUserToken BeginBindUserToken(Socket socket)
        {
            IUserToken userToken = _userTokenPool.Dequeue();
            if (userToken == null) return null;
            userToken.ReadArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            userToken.Socket = socket;
            return userToken;
        }
        /// <summary>
        /// UDP 设置usertoken
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="id"></param>
        public void EndBindUserToken(IUserToken userToken, string id)
        {
            userToken.ID = id;
            userToken.Actived = userToken.Linked = DateTimeHelper.Now;
            _sessionCache.Set(userToken.ID, userToken, _freeTime);
        }
        #endregion


        /// <summary>
        /// Active
        /// </summary>
        /// <param name="id"></param>
        public void Active(string id)
        {
            _sessionCache.Active(id, _freeTime);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public IUserToken Get(string id)
        {
            return _sessionCache.Get(id);
        }

        /// <summary>
        /// 释放IUserToken
        /// </summary>
        /// <param name="userToken"></param>
        public bool Free(IUserToken userToken)
        {
            if (userToken != null && userToken.Socket != null)
            {
                using (var loker = ObjectLock.Create("SessionManager.Free"))
                {
                    if (userToken != null && userToken.Socket != null)
                    {
                        _sessionCache.DelWithoutEvent(userToken.ID);
                        _userTokenPool.Enqueue(userToken);
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 清理全部会话
        /// </summary>
        public void Clear()
        {
            _sessionCache.Clear();
        }
    }
}
