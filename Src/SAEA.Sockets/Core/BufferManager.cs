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
*修改时间：2026/11/13 16:42:11
*修改人： yswenli
*版本号： v26.11.5.1
*描述：槽位耗尽时在硬上限内整段扩容，补偿退役令牌永久占用的槽位
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using SAEA.Common;
using SAEA.Sockets.Model;

namespace SAEA.Sockets.Core
{
    /// <summary>
    /// 缓冲区管理器，按段惰性分配并复用固定大小的缓冲区
    /// </summary>
    /// <remarks>
    /// 缓冲区按段惰性分配：构造期不再一次性申请 bufferSize * 槽位数 的连续数组，
    /// 而是每段约 1 MiB，在首次使用时逐段申请，内存占用随实际连接数增长。
    /// 槽位索引统一编号，释放时按段号还原，避免不同段的相同偏移互相串用。
    /// </remarks>
    class BufferManager : IDisposable
    {
        /// <summary>
        /// 单段目标字节数
        /// </summary>
        const int SegmentTargetBytes = 1024 * 1024;

        readonly object _syncRoot = new object();
        readonly int _bufferSize;
        /// <summary>
        /// 当前可分配的槽位总数；退役令牌会永久占住槽位，因此允许在硬上限内扩容
        /// </summary>
        int _totalSlots;
        /// <summary>
        /// 扩容硬上限，防止退役持续发生时无限申请内存
        /// </summary>
        readonly int _maxSlots;
        readonly int _slotsPerSegment;
        readonly Stack<int> _freeSlots;
        readonly List<byte[]> _segments;
        bool[] _slotInUse;

        int _nextSlot;
        bool _disposed;

        /// <summary>
        /// 初始化缓冲区管理器
        /// </summary>
        /// <param name="totalBytes"></param>
        /// <param name="bufferSize"></param>
        public BufferManager(long totalBytes, int bufferSize)
        {
            if (bufferSize <= 0) throw new ArgumentOutOfRangeException(nameof(bufferSize));

            _bufferSize = bufferSize;

            var slots = totalBytes / bufferSize;
            if (slots < 1) slots = 1;
            if (slots > int.MaxValue) slots = int.MaxValue;
            _totalSlots = (int)slots;
            _maxSlots = _totalSlots > int.MaxValue / 2 ? int.MaxValue : _totalSlots * 2;

            var slotsPerSegment = SegmentTargetBytes / bufferSize;
            if (slotsPerSegment < 1) slotsPerSegment = 1;
            if (slotsPerSegment > _totalSlots) slotsPerSegment = _totalSlots;
            _slotsPerSegment = slotsPerSegment;

            _freeSlots = new Stack<int>();
            _segments = new List<byte[]>();
            _slotInUse = new bool[_totalSlots];
        }

        /// <summary>
        /// 为事件参数分配一块缓冲区
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public bool SetBuffer(SocketAsyncEventArgs args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));

            lock (_syncRoot)
            {
                if (_disposed) return false;

                int slot;
                if (_freeSlots.Count > 0)
                {
                    slot = _freeSlots.Pop();
                }
                else if (_nextSlot < _totalSlots)
                {
                    slot = _nextSlot++;
                }
                else
                {
                    // 槽位全部占用时按需扩容：退役令牌会永久占住槽位，容量不能因此衰减
                    if (!TryGrowLocked()) return false;
                    slot = _nextSlot++;
                }

                var segmentIndex = slot / _slotsPerSegment;
                EnsureSegment(segmentIndex);

                var offset = (slot % _slotsPerSegment) * _bufferSize;
                try
                {
                    args.SetBuffer(_segments[segmentIndex], offset, _bufferSize);
                }
                catch
                {
                    // args 仍被在途操作占用时 SetBuffer 会抛异常；槽位必须立即退回，
                    // 否则每次失败都永久丢失一个缓冲区槽位，最终整池无可用缓冲区。
                    _freeSlots.Push(slot);
                    throw;
                }
                _slotInUse[slot] = true;
                return true;
            }
        }

        /// <summary>
        /// 在硬上限内扩容缓冲区槽位
        /// </summary>
        /// <remarks>
        /// 退役令牌会永久占住槽位且缓冲不能归还，容量不能因此衰减；扩容只用于补偿这部分损失，
        /// 硬上限防止退役持续发生时无界申请内存。
        /// 槽位到段的映射固定为 slot / _slotsPerSegment，且段长度是建段时的剩余槽位数，
        /// 因此只能整段追加：末尾短段若继续使用会越界，所以短段尾部槽位永久弃用，
        /// 新槽位一律从下一个段起点开始。
        /// </remarks>
        /// <returns>true 表示扩容成功；false 表示已达上限或没有完整段可追加</returns>
        bool TryGrowLocked()
        {
            var aligned = _totalSlots;
            var remainder = aligned % _slotsPerSegment;
            if (remainder != 0) aligned += _slotsPerSegment - remainder;

            if (_totalSlots >= _maxSlots || aligned >= _maxSlots)
            {
                LogHelper.Error($"BufferManager 缓冲区槽位已达扩容上限:{_totalSlots}/{_maxSlots}", new KernelException("缓冲区槽位不足。"));
                return false;
            }

            var newTotal = (int)Math.Min((long)_maxSlots, (long)aligned + _slotsPerSegment);
            Array.Resize(ref _slotInUse, newTotal);
            _totalSlots = newTotal;
            // 跳过短段尾部不可用的槽位，从整段起点继续分配
            _nextSlot = aligned;
            return true;
        }

        void EnsureSegment(int segmentIndex)
        {
            while (_segments.Count <= segmentIndex)
            {
                var index = _segments.Count;
                var remainingSlots = _totalSlots - index * _slotsPerSegment;
                var slots = Math.Min(_slotsPerSegment, remainingSlots);
                _segments.Add(new byte[(int)((long)slots * _bufferSize)]);
            }
        }

        /// <summary>
        /// FreeBuffer,若对象复用，无需返回
        /// </summary>
        /// <param name="args"></param>
        public void FreeBuffer(SocketAsyncEventArgs args)
        {
            if (args == null) return;

            lock (_syncRoot)
            {
                if (_disposed) return;

                var buffer = args.Buffer;
                if (buffer == null) return;

                // 数组按引用比较，定位到所属段后再换算全局槽位
                var segmentIndex = _segments.IndexOf(buffer);
                if (segmentIndex < 0) return;

                var slot = segmentIndex * _slotsPerSegment + (args.Offset / _bufferSize);
                if (slot < 0 || slot >= _totalSlots) return;
                if (!_slotInUse[slot]) return;

                _slotInUse[slot] = false;
                _freeSlots.Push(slot);
            }
        }

        /// <summary>
        /// 释放所有缓冲区
        /// </summary>
        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_disposed) return;
                _disposed = true;
                _freeSlots.Clear();
                _segments.Clear();
            }
        }
    }
}