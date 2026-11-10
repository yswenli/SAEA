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
*修改时间：2026/11/9 10:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：增加在途发送操作，断开时让发送 Task 可观察地失败
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
        /// 清理令牌：关闭套接字、释放发送所有权与编解码器、释放读写 args 并置空引用，可重复调用
        /// </summary>
        public virtual void Clear()
        {
            // 在途发送对应的 Task 必须可观察地失败，否则调用方会永久等待一个已不可能完成的发送。
            AbandonPendingSend(new KernelException("连接已关闭，发送未完成。"));
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