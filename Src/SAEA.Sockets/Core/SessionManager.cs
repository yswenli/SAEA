/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Sockets.Core
*文件名： SessionManager
*版本号： v26.4.23.1
*唯一标识：55af79f0-0ccb-4512-9fdf-ba28de33f856
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：SessionManager接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 20:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：UDP 接收端点模板按套接字地址族选择
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Sockets.Base;
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
        /// 空闲超时豁免的会话集合（长连接如 WebSocket 使用）
        /// </summary>
        readonly ConcurrentDictionary<string, byte> _noTimeoutIds = new ConcurrentDictionary<string, byte>();

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
        /// <param name="connectTimeOut"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public IUserToken BindUserToken(Socket socket, int connectTimeOut)
        {
            if (_semaphoreSlim.Wait(connectTimeOut))
            {
                try
                {
                    IUserToken userToken = _userTokenPool.Dequeue(connectTimeOut);
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
                // ReceiveFromAsync 要求端点模板与套接字地址族一致，IPv6 单栈套接字传 IPv4 模板会抛 ArgumentException
                userToken.ReadArgs.RemoteEndPoint = socket != null && socket.AddressFamily == AddressFamily.InterNetworkV6
                    ? new IPEndPoint(IPAddress.IPv6Any, 0)
                    : new IPEndPoint(IPAddress.Any, 0);
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
        /// 续期会话空闲超时，已登记超时豁免的会话按永不过期处理，id 为空时忽略
        /// </summary>
        /// <param name="id">会话标识</param>
        public void Active(string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            // TimeSpan.Zero 在 MemoryCache.Set 中即 DateTime.MaxValue（永不过期）
            var timeout = _noTimeoutIds.ContainsKey(id) ? TimeSpan.Zero : _freeTime;

            _sessionCache.Active(id, timeout);
        }

        /// <summary>
        /// 设置/取消会话的空闲超时豁免
        /// </summary>
        /// <param name="id">会话ID</param>
        /// <param name="noTimeout">true 表示不参与空闲清理</param>
        public void SetNoTimeout(string id, bool noTimeout)
        {
            if (string.IsNullOrEmpty(id)) return;

            if (noTimeout)
            {
                _noTimeoutIds[id] = 0;
                // 立即刷新一次，否则下一个清理周期之前仍按旧过期时间被删
                Active(id);
            }
            else
            {
                _noTimeoutIds.TryRemove(id, out _);
                // 恢复成正常的空闲超时（会话已不存在时 Active 自然 no-op）
                Active(id);
            }
        }

        /// <summary>
        /// 从会话缓存取出令牌
        /// </summary>
        /// <param name="id">会话标识</param>
        /// <returns>对应令牌，不存在或已过期时为 null</returns>
        public IUserToken Get(string id)
        {
            return _sessionCache.Get(id);
        }

        /// <summary>
        /// 释放令牌并放回池中，同时清除会话缓存与超时豁免记录
        /// </summary>
        /// <remarks>
        /// 首次调用即返回 true 并释放信号量；令牌若仍有在途内核操作，则改为延迟回收：
        /// 由最后一个完成点经 <see cref="CompleteOperation"/> 入池，避免复用时 args 仍在使用。
        /// </remarks>
        /// <param name="userToken">待释放的令牌</param>
        /// <returns>是否成功入池，令牌为 null、无会话标识或入池失败时为 false</returns>
        public bool Free(IUserToken userToken)
        {
            if (userToken == null) return false;

            try
            {
                using var locker = ObjectLock.Create("SessionManager.Free");

                // 锁内只读取一次 ID 并立即清空：并发的 Disconnect 可能同时到达同一令牌，
                // 首个调用完成入池后，后续调用若再次 Enqueue 会把同一令牌重复放回池中，
                // 导致一个令牌同时被两个连接使用，并让信号量计数超过上限。
                var sessionId = userToken.ID;
                if (string.IsNullOrEmpty(sessionId))
                    return false;

                userToken.ID = null;

                // 清理超时豁免，避免会话 ID 复用时泄漏
                _noTimeoutIds.TryRemove(sessionId, out _);
                _sessionCache.DelWithoutEvent(sessionId);

                try
                {
                    // 非内置实现没有在途操作计数，退回立即入池。
                    var baseUserToken = userToken as BaseUserToken;
                    if (baseUserToken != null)
                    {
                        // 先关闭 socket：关闭会让内核立即取消在途接收/发送并投递完成回调，
                        // 否则延迟回收要等对端下一次发包，令牌与 socket 会长期滞留在途。
                        try
                        {
                            if (userToken.Socket != null && userToken.Socket.Connected)
                                userToken.Socket.Close();
                        }
                        catch { }

                        if (!baseUserToken.RequestRecycle())
                        {
                            // 仍有在途接收/发送：此刻入池会让下一次复用的 SetBuffer 抛异常并永久丢失令牌，
                            // 因此交给最后一个完成点入池；本次仍返回 true，保持计数与断线回调只触发一次。
                            return true;
                        }
                    }
                    return _userTokenPool.Enqueue(userToken);
                }
                finally
                {
                    // 入池失败也必须释放信号量，否则会导致死锁
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
            }
            catch (Exception ex)
            {
                LogHelper.Error("SessionManager.Free 错误", ex);
            }
            return false;
        }

        /// <summary>
        /// 退役令牌：令牌永不复用，池容量改由后续取用时补建
        /// </summary>
        /// <param name="userToken">待退役的令牌</param>
        internal void RetireUserToken(IUserToken userToken)
        {
            // 不走 Free 的幂等分支：看门狗放弃时 ID 可能已被置空，Free 会直接返回 false
            _userTokenPool.Retire(userToken);
        }

        /// <summary>
        /// 结束令牌上一次内核操作；延迟回收的令牌在最后一个操作结束时入池
        /// </summary>
        /// <param name="userToken">完成回调所属的令牌</param>
        internal void CompleteOperation(IUserToken userToken)
        {
            var baseUserToken = userToken as BaseUserToken;
            if (baseUserToken == null) return;

            try
            {
                // ID 已置空、信号量已释放，这里只做入池，不再走 Free 的幂等分支
                if (baseUserToken.EndOperation())
                {
                    _userTokenPool.Enqueue(baseUserToken);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error($"SessionManager.CompleteOperation 错误:{baseUserToken.ID}/{baseUserToken.Guid}", ex);
            }
        }

        /// <summary>
        /// 清理全部会话
        /// </summary>
        public void Clear()
        {
            _sessionCache.Clear();
            _noTimeoutIds.Clear();
        }
    }
}