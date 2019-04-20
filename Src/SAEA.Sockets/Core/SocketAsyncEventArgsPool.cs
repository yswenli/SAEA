/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets
*文件名： SocketAsyncEventArgsPool
*版本号： v4.3.3.7
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
*版本号： v4.3.3.7
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;

namespace SAEA.Sockets.Core
{
    class SocketAsyncEventArgsPool
    {
        ConcurrentQueue<SocketAsyncEventArgs> _argsPool;

        int _capacity = 1000 * 100;

        EventHandler<SocketAsyncEventArgs> _completed;


        public SocketAsyncEventArgsPool(int capacity = 1000 * 100)
        {
            _capacity = capacity;
            _argsPool = new ConcurrentQueue<SocketAsyncEventArgs>();
        }


        public void InitPool(EventHandler<SocketAsyncEventArgs> completed)
        {
            _completed = completed;
            for (int i = 0; i < _capacity; i++)
            {
                var args = new SocketAsyncEventArgs();
                _argsPool.Enqueue(args);
            }
        }

        public void Enqueue(SocketAsyncEventArgs args)
        {
            if (args == null) { throw new ArgumentNullException("Items added to a SocketAsyncEventArgsPool cannot be null"); }
            args.UserToken = null;
            args.Completed -= _completed;
            _argsPool.Enqueue(args);
        }


        public SocketAsyncEventArgs Dequeue()
        {
            SocketAsyncEventArgs args;
            while (!_argsPool.TryDequeue(out args))
            {
                Thread.Sleep(1);
            }
            args.Completed += _completed;
            return args;
        }


        public int Count
        {
            get { return _argsPool.Count; }
        }

    }
}
