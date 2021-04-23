/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets.Core
*文件名： UserTokenPool
*版本号： v6.0.0.1
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
*版本号： v6.0.0.1
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Common.Threading;
using SAEA.Sockets.Interface;

using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;

namespace SAEA.Sockets.Core
{
    /// <summary>
    /// UserTokenPool
    /// </summary>
    public class UserTokenPool : IDisposable
    {
        BufferManager _bufferManager;

        UserTokenFactory _userTokenFactory;

        ConcurrentQueue<IUserToken> _concurrentQueue = new ConcurrentQueue<IUserToken>();

        /// <summary>
        /// UserTokenPool
        /// </summary>
        /// <param name="context"></param>
        /// <param name="count"></param>
        /// <param name="bufferSize"></param>
        /// <param name="completed"></param>
        public UserTokenPool(IContext context, int count, int bufferSize, EventHandler<SocketAsyncEventArgs> completed)
        {
            _userTokenFactory = new UserTokenFactory();

            _bufferManager = new BufferManager(bufferSize * count, bufferSize);

            for (int i = 0; i < count; i++)
            {
                IUserToken userToken = _userTokenFactory.Create(context);

                var writeArgs = new SocketAsyncEventArgs();
                writeArgs.Completed += completed;
                userToken.WriteArgs = writeArgs;

                var readArgs = new SocketAsyncEventArgs();
                readArgs.Completed += completed;
                _bufferManager.SetBuffer(readArgs);
                userToken.ReadArgs = readArgs;

                userToken.ReadArgs.UserToken = userToken.WriteArgs.UserToken = userToken;
                _concurrentQueue.Enqueue(userToken);
            }
        }

        /// <summary>
        /// Dequeue
        /// </summary>
        /// <returns></returns>
        public IUserToken Dequeue()
        {
            IUserToken token;
            while (!_concurrentQueue.TryDequeue(out token) || token == null)
            {
                Thread.Yield();
            }
            return token;
        }

        /// <summary>
        /// Enqueue
        /// </summary>
        /// <param name="userToken"></param>
        public void Enqueue(IUserToken userToken)
        {
            var socket = userToken.Socket;
            userToken.Socket = null;
            try
            {
                socket.Close();
            }
            catch { }
            _concurrentQueue.Enqueue(userToken);
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            _concurrentQueue.Clear();
            _bufferManager.Dispose();
        }
    }
}
