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
*命名空间：SAEA.Sockets.Core
*文件名： UserTokenPool
*版本号： v7.0.0.1
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/10/26 15:54:21
*描述：
*
*=====================================================================
*修改标记
*创建时间：2018/10/26 15:54:21
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
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
    public class UserTokenPool : IDisposable
    {
        BufferManager _bufferManager;
        UserTokenFactory _userTokenFactory;
        ConcurrentQueue<IUserToken> _concurrentQueue;

        int _maxCount = 100;

        /// <summary>
        /// 初始化UserTokenPool实例
        /// </summary>
        /// <param name="context">上下文对象</param>
        /// <param name="maxCount">最大用户令牌数量</param>
        /// <param name="bufferSize">缓冲区大小</param>
        /// <param name="completed">Socket操作完成事件处理程序</param>
        public UserTokenPool(IContext<ICoder> context, int maxCount, int bufferSize, EventHandler<SocketAsyncEventArgs> completed)
        {
            _maxCount = maxCount;
            _userTokenFactory = new UserTokenFactory();
            _bufferManager = new BufferManager(bufferSize * maxCount, bufferSize);
            _concurrentQueue = new ConcurrentQueue<IUserToken>();
            for (int i = 0; i < maxCount; i++)
            {
                IUserToken userToken = _userTokenFactory.Create(context);

                var writeArgs = new SocketAsyncEventArgs();
                writeArgs.Completed += completed;
                userToken.WriteArgs = writeArgs;
                var readArgs = new SocketAsyncEventArgs();
                readArgs.Completed += completed;
                userToken.ReadArgs = readArgs;
                userToken.ReadArgs.UserToken = userToken.WriteArgs.UserToken = userToken;
                _concurrentQueue.Enqueue(userToken);
            }
        }

        /// <summary>
        /// 从池中取出一个用户令牌
        /// </summary>
        /// <param name="timeOut">超时时间</param>
        /// <returns>用户令牌</returns>
        public IUserToken Dequeue(int timeOut = 1000)
        {
            IUserToken userToken;
            _concurrentQueue.TryDequeue(out userToken);
            if (userToken != null)
                _bufferManager.SetBuffer(userToken.ReadArgs);
            else
            {
                Thread.Sleep(100);
                return Dequeue(timeOut);
            }
            return userToken;
        }

        /// <summary>
        /// 将用户令牌放回池中
        /// </summary>
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
                    _bufferManager.FreeBuffer(userToken.ReadArgs);
                userToken.ReleaseWrite();
                _concurrentQueue.Enqueue(userToken);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _concurrentQueue.Clear();
            _bufferManager.Dispose();
        }
    }
}
