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
*文件名： UserTokenPool
*版本号： v26.4.23.1
*唯一标识：5d1b335c-0895-4ae4-a6c2-4f660e55479d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/26 16:06:37
*描述：UserTokenPool池类
*
*=====================================================================
*修改标记
*修改时间：2018/10/26 16:06:37
*修改人： yswenli
*版本号： v26.4.23.1
*描述：UserTokenPool池类
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;

using SAEA.Common;
using SAEA.Sockets.Interface;

namespace SAEA.Sockets.Core
{
    /// <summary>
    /// 用户令牌池类，用于管理用户令牌的创建和回收
    /// </summary>
    /// <remarks>
    /// 令牌按需创建：构造期只预热少量令牌，其余在 <see cref="Dequeue"/> 时按需补齐，
    /// 避免在未建立连接时就为 MaxConnects 个连接预分配 SocketAsyncEventArgs 与缓冲区。
    /// 缓冲区同样由 <see cref="BufferManager"/> 分块惰性申请。
    /// </remarks>
    public class UserTokenPool : IDisposable
    {
        /// <summary>
        /// 预热令牌数量，用于摊薄首个连接的创建开销
        /// </summary>
        const int WarmUpCount = 16;

        readonly object _syncRoot = new object();
        readonly int _maxCount;
        readonly IContext<ICoder> _context;
        readonly EventHandler<SocketAsyncEventArgs> _completed;

        BufferManager _bufferManager;
        UserTokenFactory _userTokenFactory;
        ConcurrentQueue<IUserToken> _concurrentQueue;

        int _createdCount;
        bool _disposed;

        /// <summary>
        /// 初始化UserTokenPool实例
        /// </summary>
        /// <param name="context">上下文对象</param>
        /// <param name="maxCount">最大用户令牌数量</param>
        /// <param name="bufferSize">缓冲区大小</param>
        /// <param name="completed">Socket操作完成事件处理程序</param>
        public UserTokenPool(IContext<ICoder> context, int maxCount, int bufferSize, EventHandler<SocketAsyncEventArgs> completed)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (maxCount <= 0) throw new ArgumentOutOfRangeException(nameof(maxCount));

            _context = context;
            _maxCount = maxCount;
            _completed = completed;
            _userTokenFactory = new UserTokenFactory();
            _bufferManager = new BufferManager((long)bufferSize * maxCount, bufferSize);
            _concurrentQueue = new ConcurrentQueue<IUserToken>();

            var warmUpCount = Math.Min(maxCount, WarmUpCount);
            for (int i = 0; i < warmUpCount; i++)
            {
                _concurrentQueue.Enqueue(CreateUserToken());
                _createdCount++;
            }
        }

        IUserToken CreateUserToken()
        {
            IUserToken userToken = _userTokenFactory.Create(_context);

            var writeArgs = new SocketAsyncEventArgs();
            writeArgs.Completed += _completed;
            userToken.WriteArgs = writeArgs;

            var readArgs = new SocketAsyncEventArgs();
            readArgs.Completed += _completed;
            userToken.ReadArgs = readArgs;

            userToken.ReadArgs.UserToken = userToken.WriteArgs.UserToken = userToken;
            return userToken;
        }

        IUserToken PrepareUserToken(IUserToken userToken)
        {
            _bufferManager.SetBuffer(userToken.ReadArgs);
            // 重新设置 UserToken，因为 Enqueue 时被设为 null
            if (userToken.ReadArgs != null)
                userToken.ReadArgs.UserToken = userToken;
            if (userToken.WriteArgs != null)
                userToken.WriteArgs.UserToken = userToken;
            return userToken;
        }

        /// <summary>
        /// 从池中取出一个用户令牌
        /// </summary>
        /// <param name="timeOut">超时时间</param>
        /// <returns>用户令牌</returns>
        public IUserToken Dequeue(int timeOut = 1000)
        {
            DateTime startTime = DateTime.Now;
            while (true)
            {
                IUserToken userToken;
                if (_concurrentQueue.TryDequeue(out userToken))
                {
                    return PrepareUserToken(userToken);
                }

                lock (_syncRoot)
                {
                    if (_disposed) return null;

                    // 进入锁后再取一次，避免 Enqueue 与按需创建之间丢失唤醒
                    if (_concurrentQueue.TryDequeue(out userToken))
                    {
                        return PrepareUserToken(userToken);
                    }

                    // 池中暂无空闲令牌但尚未达到上限时，按需创建
                    if (_createdCount < _maxCount)
                    {
                        userToken = CreateUserToken();
                        _createdCount++;
                        return PrepareUserToken(userToken);
                    }

                    // 已达到上限且无空闲令牌，等待归还
                    var remaining = timeOut - (int)(DateTime.Now - startTime).TotalMilliseconds;
                    if (remaining <= 0)
                    {
                        return null;
                    }
                    Monitor.Wait(_syncRoot, remaining);
                }
            }
        }

        /// <summary>
        /// 将用户令牌放回池中
        /// </summary>
        /// <remarks>
        /// 回收前会主动放弃在途发送缓冲的所有权且不释放：内核可能仍在读取该内存，
        /// 若将其归还池化数组并再次租用会造成 use-after-return，因此此处有意交由 GC 回收，
        /// 与 SaeaServerSocket.AbandonSendingOwner 的处理一致。
        /// </remarks>
        /// <param name="userToken">用户令牌</param>
        /// <returns>是否成功放回</returns>
        public bool Enqueue(IUserToken userToken)
        {
            if (_concurrentQueue.Count >= _maxCount) return false;
            if (userToken != null)
            {
                var socket = userToken.Socket;
                try
                {
                    if (socket != null)
                    {
                        if (socket.Connected)
                            socket?.Close();
                        userToken.Socket = null;
                    }
                }
                catch { }
                if (userToken.ReadArgs != null)
                {
                    _bufferManager.FreeBuffer(userToken.ReadArgs);
                    userToken.ReadArgs.UserToken = null;
                    // 重置ReadArgs的RemoteEndPoint（如果有）
                    if (userToken.ReadArgs.RemoteEndPoint != null)
                    {
                        userToken.ReadArgs.RemoteEndPoint = null;
                    }
                }
                if (userToken.WriteArgs != null)
                {
                    // 重置WriteArgs的缓冲区
                    userToken.WriteArgs.SetBuffer(null, 0, 0);
                    userToken.WriteArgs.UserToken = null;
                    // 重置WriteArgs的RemoteEndPoint（如果有）
                    if (userToken.WriteArgs.RemoteEndPoint != null)
                    {
                        userToken.WriteArgs.RemoteEndPoint = null;
                    }
                }
                // 清理Coder对象
                if (userToken.Coder != null)
                {
                    userToken.Coder.Clear();
                    // 不要设置为null，因为需要重复使用
                }
                // 重置其他属性
                userToken.ID = null;
                userToken.Linked = DateTime.MinValue;
                userToken.Actived = DateTime.MinValue;
                try { userToken.TakeSendingOwner(); } catch { }
                userToken.ReleaseWrite();
                _concurrentQueue.Enqueue(userToken);

                // 唤醒等待空闲令牌的 Dequeue 调用
                lock (_syncRoot)
                {
                    Monitor.Pulse(_syncRoot);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_disposed) return;
                _disposed = true;
                _concurrentQueue.Clear();
                Monitor.PulseAll(_syncRoot);
            }
            _bufferManager.Dispose();
        }
    }
}