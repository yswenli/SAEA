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
*命名空间：SAEA.Sockets.Base
*文件名： BaseUserToken
*版本号： v26.4.23.1
*唯一标识：26c02e3f-743e-4e02-b007-5a1d1c447f96
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/08/21 19:42:03
*描述：BaseUserToken令牌类
*
*=====================================================================
*修改标记
*修改时间：2026/11/24 18:12:35
*修改人： yswenli
*版本号： 
*描述：v26.11.24.2
*
*****************************************************************************/
using System;
using System.Net.Sockets;
using System.Threading;

using SAEA.Common;
using SAEA.Sockets.Core;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

namespace SAEA.Sockets.Base
{
    /// <summary>
    /// 默认连接令牌：持有套接字、读写 SocketAsyncEventArgs、编解码器与写锁，供 TCP/UDP 收发实现复用
    /// </summary>
    public class BaseUserToken : IUserToken
    {
        /// <summary>
        /// 写锁信号量，保证同一连接上的发送串行化
        /// </summary>
        protected readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1, 1);
        /// <summary>
        /// 发送进行中标志的后备字段，经 IsSending 属性读写
        /// </summary>
        protected bool _isSending = false;

        /// <summary>
        /// 回收占位标志：置位后不再提交新的内核操作
        /// </summary>
        const int RecycleRequestedBit = 1;

        /// <summary>
        /// 在途操作数在状态中的增量，低位留给回收占位标志
        /// </summary>
        const int OperationUnit = 2;

        /// <summary>
        /// 令牌状态：低 1 位为回收已请求，其余位为在途内核操作数
        /// </summary>
        int _recycleState;

        /// <summary>
        /// 退役标志：置位后令牌永不复用、永不归还池化缓冲
        /// </summary>
        int _retired;

        /// <summary>
        /// 绑定代次：每次绑定到新连接时自增，用于丢弃上一代连接遗留的断开调用
        /// </summary>
        long _generation;

        /// <summary>
        /// 接收在途标志：1 表示已提交接收且尚未回调完成；同一连接不允许重复提交接收
        /// </summary>
        int _receiving;

        /// <summary>
        /// 创建令牌并生成唯一标识
        /// </summary>
        public BaseUserToken()
        {
            Guid = System.Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// 连接唯一标识，构造时生成 32 位无连字符形式，之后不变
        /// </summary>
        public string Guid { get; private set; }

        /// <summary>
        /// 会话标识，服务端取对端地址字符串、客户端取本地地址字符串
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// 连接的套接字对象，Clear 后为 null
        /// </summary>
        public Socket Socket { get; set; }

        /// <summary>
        /// 当前绑定代次，0 表示尚未绑定过
        /// </summary>
        public long Generation
        {
            get { return Volatile.Read(ref _generation); }
        }

        /// <summary>
        /// 读取操作的 SocketAsyncEventArgs，Clear 后为 null
        /// </summary>
        public SocketAsyncEventArgs ReadArgs { get; set; }

        /// <summary>
        /// 写入操作的 SocketAsyncEventArgs，Clear 后为 null
        /// </summary>
        public SocketAsyncEventArgs WriteArgs { get; set; }

        /// <summary>
        /// 连接建立时间
        /// </summary>
        public DateTime Linked { get; set; }

        /// <summary>
        /// 最近一次活动时间
        /// </summary>
        public DateTime Actived { get; set; }

        /// <summary>
        /// 该连接使用的编解码器，Clear 时一并清空
        /// </summary>
        public ICoder Coder { get; set; }

        IDisposable _sendingOwner;

        /// <summary>
        /// 当前在途的发送操作；TCP 发送完成、失败或断开时据此完成等待方。
        /// </summary>
        private SendOperation _pendingSend;

        /// <summary>
        /// 当前在途的发送操作，保证一次连接同一时刻只有一个发送持有写闸门。
        /// </summary>
        internal SendOperation PendingSend
        {
            get { return Volatile.Read(ref _pendingSend); }
            set { Volatile.Write(ref _pendingSend, value); }
        }

        /// <summary>
        /// 原子取出并清空在途发送操作，取出后由调用方负责完成或放弃。
        /// </summary>
        /// <returns>在途发送操作；不存在时为 null</returns>
        internal SendOperation TakePendingSend()
        {
            return Interlocked.Exchange(ref _pendingSend, null);
        }

        /// <summary>
        /// 以失败结束在途发送（断开、令牌回收等场景）；没有在途发送时忽略。
        /// </summary>
        /// <param name="exception">失败原因</param>
        internal void AbandonPendingSend(Exception exception)
        {
            var operation = TakePendingSend();
            if (operation == null) return;
            operation.TryCompleteException(exception ?? new KernelException("连接已断开，发送未完成。"));
        }

        /// <summary>
        /// 发送缓冲区所有权对象：零拷贝直发时为 null，库内租用池化缓冲时由发送完成回调负责释放
        /// </summary>
        public IDisposable SendingOwner
        {
            get { return _sendingOwner; }
            set { Volatile.Write(ref _sendingOwner, value); }
        }

        /// <summary>
        /// 原子取出并清空发送缓冲区所有权对象，取出后由调用方释放，保证恰好释放一次
        /// </summary>
        /// <returns>所有权对象，未持有时为 null</returns>
        public IDisposable TakeSendingOwner()
        {
            return Interlocked.Exchange(ref _sendingOwner, null);
        }

        /// <summary>
        /// 是否有发送正在进行
        /// </summary>
        public bool IsSending
        {
            get { return _isSending; }
            set { _isSending = value; }
        }

        /// <summary>
        /// 等待写锁，超时返回 false
        /// </summary>
        /// <param name="timeout">超时时间（毫秒）</param>
        /// <returns>是否取得写锁</returns>
        public bool WaitWrite(int timeout)
        {
            return _writeSemaphore.Wait(timeout);
        }

        /// <summary>
        /// 释放写锁，未持有时忽略多余释放
        /// </summary>
        public void ReleaseWrite()
        {
            try { _writeSemaphore.Release(); }
            catch (SemaphoreFullException) { }
        }

        /// <summary>
        /// 推进绑定代次并返回新值
        /// </summary>
        /// <returns>自增后的代次</returns>
        internal long NextGeneration()
        {
            return Interlocked.Increment(ref _generation);
        }

        /// <summary>
        /// 登记一次即将提交的内核操作；已请求回收时拒绝登记，调用方不得再提交。
        /// </summary>
        /// <returns>true 表示可以提交，提交后必须由完成点调用 <see cref="EndOperation"/></returns>
        internal bool TryBeginOperation()
        {
            if (Volatile.Read(ref _retired) != 0) return false;

            // netstandard2.0 没有 Interlocked.Or，只能按 CAS 循环维护「回收位 + 在途计数」的复合状态
            var state = Volatile.Read(ref _recycleState);
            while (true)
            {
                if ((state & RecycleRequestedBit) != 0) return false;

                var next = state + OperationUnit;
                var observed = Interlocked.CompareExchange(ref _recycleState, next, state);
                if (observed == state) return true;

                state = observed;
            }
        }

        /// <summary>
        /// 结束一次已登记的内核操作；回收已请求且计数归零时返回 true（仅返回一次）。
        /// </summary>
        /// <returns>true 表示此刻可以安全入池</returns>
        internal bool EndOperation()
        {
            var state = Volatile.Read(ref _recycleState);
            while (true)
            {
                if ((state >> 1) == 0)
                {
                    // 提交与完成必须一一配对；计数已为 0 时再减会让状态反转成巨大的正数，
                    // 从此再也回不到 0 而永久拒绝入池，因此这里只记录错误并忽略。
                    LogHelper.Error($"BaseUserToken.EndOperation 在途操作计数已为 0:{ID}/{Guid}", new KernelException("令牌操作计数异常。"));
                    return false;
                }

                var next = state - OperationUnit;
                var observed = Interlocked.CompareExchange(ref _recycleState, next, state);
                if (observed == state)
                {
                    return (next & RecycleRequestedBit) != 0 && (next >> 1) == 0;
                }

                state = observed;
            }
        }

        /// <summary>
        /// 请求回收：置回收位并停止接受新操作。
        /// </summary>
        /// <returns>true 表示当前已无在途操作，调用方应立即入池；false 表示由最后一个完成点入池</returns>
        internal bool RequestRecycle()
        {
            // 退役令牌的 args 与缓冲可能仍被内核持有，一旦入池就会被新连接复用，
            // 因此这里直接拒绝，连回收位都不改。
            if (Volatile.Read(ref _retired) != 0) return false;

            var state = Volatile.Read(ref _recycleState);
            while (true)
            {
                // 已请求过就返回 false，避免同一令牌被两条路径重复入池
                if ((state & RecycleRequestedBit) != 0) return false;

                var next = state | RecycleRequestedBit;
                var observed = Interlocked.CompareExchange(ref _recycleState, next, state);
                if (observed == state)
                {
                    return (state >> 1) == 0;
                }

                state = observed;
            }
        }

        /// <summary>
        /// 复位操作状态，供令牌投入新连接前调用；状态非法时拒绝并保持原值。
        /// </summary>
        /// <remarks>
        /// 允许复用的状态只有两种来源：池内令牌（状态为 <see cref="RecycleRequestedBit"/>，
        /// 即回收已请求且在途计数已归零）与从未使用的新令牌（状态为 0）。
        /// 其它取值说明令牌仍在途却被放进了池中，清零后复用会让同一组 args 被两个连接同时使用，
        /// 因此这里不做修改并返回 false。
        /// </remarks>
        /// <returns>true 表示状态合法并已清零；false 表示令牌仍在途，未做任何修改</returns>
        internal bool TryResetForReuse()
        {
            // 退役令牌不复用：清零状态会让被放弃的内核操作与新连接共用同一组 args
            if (Volatile.Read(ref _retired) != 0) return false;

            var state = Volatile.Read(ref _recycleState);
            while (true)
            {
                if (state != 0 && state != RecycleRequestedBit) return false;

                var observed = Interlocked.CompareExchange(ref _recycleState, 0, state);
                if (observed == state) return true;

                state = observed;
            }
        }

        /// <summary>
        /// 当前操作状态原始值：低 1 位为回收已请求，其余位为在途操作数
        /// </summary>
        internal int OperationState
        {
            get { return Volatile.Read(ref _recycleState); }
        }

        /// <summary>
        /// 令牌是否已退役；退役后永不复用，其 args 与池化缓冲也不得归还
        /// </summary>
        internal bool IsRetired
        {
            get { return Volatile.Read(ref _retired) != 0; }
        }

        /// <summary>
        /// 将令牌标记为退役，仅首次调用生效
        /// </summary>
        /// <remarks>
        /// 用于发送看门狗放弃等待的场景：内核可能仍在读该令牌的发送缓冲，
        /// 复用会让新连接与残留操作共用同一组 args，因此令牌只能永久退场。
        /// </remarks>
        /// <returns>true 表示本次完成退役；false 表示此前已退役</returns>
        internal bool TryRetire()
        {
            return Interlocked.CompareExchange(ref _retired, 1, 0) == 0;
        }

        /// <summary>
        /// 尝试占用接收在途标志。
        /// </summary>
        /// <returns>此前没有在途接收时返回 true，由本次调用方提交接收</returns>
        internal bool TryBeginReceive()
        {
            return Interlocked.CompareExchange(ref _receiving, 1, 0) == 0;
        }

        /// <summary>
        /// 释放接收在途标志，允许下一次提交接收；可重复调用。
        /// </summary>
        internal void EndReceive()
        {
            Interlocked.Exchange(ref _receiving, 0);
        }

        /// <summary>
        /// 清理令牌：关闭套接字、释放发送所有权与编解码器、释放读写 args 并置空引用，可重复调用
        /// </summary>
        public virtual void Clear()
        {
            // 在途发送对应的 Task 必须可观察地失败，否则调用方会永久等待一个已不可能完成的发送。
            AbandonPendingSend(new KernelException("连接已关闭，发送未完成。"));

            // 回收后的令牌会绑定新连接：残留的在途标志会让新连接永远提交不了接收。
            EndReceive();

            Socket?.Close();
            try { TakeSendingOwner()?.Dispose(); } catch { }
            Coder?.Clear();
            ReadArgs?.Dispose();
            WriteArgs?.Dispose();
            Socket = null;
            ReadArgs = null;
            WriteArgs = null;
        }
    }
}