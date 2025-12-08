/****************************************************************************
* 
 ____    _    _____    _      ____             _        _   
/ ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
\___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
 ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
|____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|


*Copyright (c)  yswenli All Rights Reserved.
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
using System.Threading;

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

        SemaphoreSlim _semaphoreSlim;

        /// <summary>
        /// 构造会话管理器
        /// </summary>
        /// <param name="context"></param>
        /// <param name="bufferSize"></param>
        /// <param name="maxConnects"></param>
        /// <param name="completed"></param>
        /// <param name="freetime"></param>
        public SessionManager(IContext<ICoder> context, int bufferSize, int maxConnects, EventHandler<SocketAsyncEventArgs> completed, TimeSpan freetime)
        {
            _sessionCache = new MemoryCache<IUserToken>((int)freetime.TotalSeconds);

            _freeTime = freetime;

            _userTokenPool = new UserTokenPool(context, maxConnects, bufferSize, completed);

            _sessionCache.OnChanged += _sessionCache_OnChanged;

            _semaphoreSlim = new SemaphoreSlim(maxConnects, maxConnects);
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
            if (_semaphoreSlim.Wait(timeOut))
            {
                try
                {
                    IUserToken userToken = _userTokenPool.Dequeue(timeOut);
                    if (userToken == null)
                        throw new Exception("UserToken池中资源已耗尽");

                    userToken.Socket = socket;
                    userToken.ID = socket.RemoteEndPoint.ToString();
                    userToken.Actived = userToken.Linked = DateTimeHelper.Now;
                    _sessionCache.Set(userToken.ID, userToken, _freeTime);
                    return userToken;
                }
                catch
                {
                    try
                    {
                        _semaphoreSlim.Release();
                    }
                    catch (SemaphoreFullException)
                    {
                        // 忽略信号量已满的异常，防止计数超过最大限制
                        LogHelper.Info("SessionManager.BindUserToken 信号量已满，忽略释放操作");
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error("SessionManager.BindUserToken 释放信号量错误", ex);
                    }
                    throw;
                }
            }
            else
            {
                throw new Exception("UserToken池中资源已耗尽");
            }
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
            _semaphoreSlim.Wait();
            try
            {
                IUserToken userToken = _userTokenPool.Dequeue();
                if (userToken == null) return null;
                userToken.ReadArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                userToken.Socket = socket;
                return userToken;
            }
            catch
            {
                try
                {
                    _semaphoreSlim.Release();
                }
                catch (SemaphoreFullException)
                {
                    // 忽略信号量已满的异常，防止计数超过最大限制
                    LogHelper.Info("SessionManager.BeginBindUserToken 信号量已满，忽略释放操作");
                }
                catch (Exception ex)
                {
                    LogHelper.Error("SessionManager.BeginBindUserToken 释放信号量错误", ex);
                }
                throw;
            }
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
            using var locker = ObjectLock.Create("SessionManager.Free");
            if (userToken != null)
            {
                // 无论userToken是否在缓存中，都将其放回池中并释放信号量
                _sessionCache.DelWithoutEvent(userToken.ID);
                try
                {
                    _userTokenPool.Enqueue(userToken);
                }
                finally
                {
                    // 无论是否能将userToken放回池中，都必须释放信号量，否则会导致死锁
                    try
                    {
                        _semaphoreSlim.Release();
                    }
                    catch (SemaphoreFullException)
                    {
                        // 忽略信号量已满的异常，防止计数超过最大限制
                        LogHelper.Info("SessionManager.Free 信号量已满，忽略释放操作");
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error("SessionManager.Free 释放信号量错误", ex);
                    }
                }
                return true;
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
