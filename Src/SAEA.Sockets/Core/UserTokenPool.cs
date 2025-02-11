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
using System.Net.Sockets;
using System.Threading;

using SAEA.Common.Caching;
using SAEA.Sockets.Interface;

namespace SAEA.Sockets.Core
{
    public class UserTokenPool : IDisposable
    {
        BufferManager _bufferManager;
        UserTokenFactory _userTokenFactory;
        ThreadQueue<IUserToken> _concurrentQueue = new ThreadQueue<IUserToken>();

        int _maxCount = 100;

        public UserTokenPool(IContext<ICoder> context, int maxCount, int bufferSize, EventHandler<SocketAsyncEventArgs> completed)
        {
            _maxCount = maxCount;
            _userTokenFactory = new UserTokenFactory();
            _bufferManager = new BufferManager(bufferSize * maxCount, bufferSize);

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

        public IUserToken Dequeue(int timeOut = 1000)
        {
            var token = _concurrentQueue.Dequeue(timeOut);
            if (token != null && token.ReadArgs != null)
                _bufferManager.SetBuffer(token.ReadArgs);
            return token;
        }


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
                _concurrentQueue.Enqueue(userToken);
                return true;
            }
            return false;
        }

        public void Dispose()
        {
            _concurrentQueue.Clear();
            _bufferManager.Dispose();
        }
    }
}
