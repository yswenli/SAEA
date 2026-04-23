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
*文件名： BufferManager
*版本号： v26.4.23.1
*唯一标识：52d91456-599b-4d7b-b3a8-08ecda36567a
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/09/08 01:49:18
*描述：BufferManager管理类
*
*=====================================================================
*修改标记
*修改时间：2018/09/08 01:49:18
*修改人： yswenli
*版本号： v26.4.23.1
*描述：BufferManager管理类
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;

using SAEA.Common;

namespace SAEA.Sockets.Core
{
    /// <summary>
    /// BufferManager
    /// </summary>
    class BufferManager : IDisposable
    {
        long _numBytes;
        byte[] _buffer;
        Stack<int> _freeIndexPool;
        int _currentIndex;
        int _bufferSize;

        /// <summary>
        /// BufferManager
        /// </summary>
        /// <param name="totalBytes"></param>
        /// <param name="bufferSize"></param>
        public BufferManager(long totalBytes, int bufferSize)
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

        #region Static Factory

        /// <summary>
        /// BufferManager 缓存字典
        /// </summary>
        private static readonly ConcurrentDictionary<int, BufferManager> _managers = new ConcurrentDictionary<int, BufferManager>();

        /// <summary>
        /// 根据缓冲区大小获取或创建共享的 BufferManager
        /// </summary>
        /// <param name="bufferSize">缓冲区大小</param>
        /// <returns>BufferManager 实例</returns>
        /// <remarks>
        /// 此方法用于在 UserTokenFactory 中共享 BufferManager 实例，
        /// 避免为每个连接创建独立的缓冲区，减少内存占用。
        /// </remarks>
        public static BufferManager GetOrCreate(int bufferSize)
        {
            return _managers.GetOrAdd(bufferSize, size =>
            {
                // 根据缓冲区大小决定总字节数
                // 64KB 缓冲区：预分配 100 个（约 6.4MB）
                // 其他大小：按比例调整数量
                int count;
                if (size <= 64 * 1024)
                {
                    count = 100;
                }
                else if (size <= 256 * 1024)
                {
                    count = 50;
                }
                else
                {
                    count = 20;
                }

                long totalBytes = (long)size * count;
                return new BufferManager(totalBytes, size);
            });
        }

        /// <summary>
        /// 清除所有缓存的 BufferManager 实例
        /// </summary>
        /// <remarks>
        /// 主要用于测试或在需要释放内存时调用
        /// </remarks>
        internal static void ClearCache()
        {
            foreach (var manager in _managers.Values)
            {
                manager.Dispose();
            }
            _managers.Clear();
        }

        #endregion
    }
}