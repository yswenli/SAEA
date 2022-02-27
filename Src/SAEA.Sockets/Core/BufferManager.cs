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
*文件名： BufferManager
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
using SAEA.Common;

using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace SAEA.Sockets.Core
{
    /// <summary>
    /// BufferManager
    /// </summary>
    class BufferManager : IDisposable
    {
        int _numBytes;
        byte[] _buffer;
        Stack<int> _freeIndexPool;
        int _currentIndex;
        int _bufferSize;

        /// <summary>
        /// BufferManager
        /// </summary>
        /// <param name="totalBytes"></param>
        /// <param name="bufferSize"></param>
        public BufferManager(int totalBytes, int bufferSize)
        {
            _numBytes = totalBytes;
            _currentIndex = 0;
            _bufferSize = bufferSize;
            _freeIndexPool = new Stack<int>();
            _buffer = new byte[_numBytes];
        }

        /// <summary>
        /// SetBuffer
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public bool SetBuffer(SocketAsyncEventArgs args)
        {
            if (_freeIndexPool.Count > 0)
            {
                args.SetBuffer(_buffer, _freeIndexPool.Pop(), _bufferSize);
            }
            else
            {
                if ((_numBytes - _bufferSize) < _currentIndex)
                {
                    return false;
                }
                args.SetBuffer(_buffer, _currentIndex, _bufferSize);
                _currentIndex += _bufferSize;
            }
            return true;
        }

        /// <summary>
        /// FreeBuffer,若对象复用，无需返回
        /// </summary>
        /// <param name="args"></param>
        public void FreeBuffer(SocketAsyncEventArgs args)
        {
            if (args != null)
                _freeIndexPool.Push(args.Offset);
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            _buffer.Clear();
        }
    }
}
