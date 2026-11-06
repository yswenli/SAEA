/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.Sockets.Pipeline
*文件名： EncodedBody
*版本号： v26.11.5.1
*唯一标识：737f6e6a-12e0-4d5a-9875-0ba4e62e0469
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：出站中间体：已编码的 body 与它的类型码
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：出站中间体：已编码的 body 与它的类型码
*
*****************************************************************************/
using System;
using System.Threading;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 出站中间体：已编码的 body 与它的类型码。只在出站链内部传递，
    /// 由 <see cref="CodecEncoderHandler"/> 产出，交给 <see cref="FrameEncoderHandler"/> 补包头后即可回收。
    /// <para>
    /// 所有权约定：创建者持有 <see cref="Owner"/>，在下游返回（或抛异常）后立刻 <see cref="Dispose"/>；
    /// 下游只读取 <see cref="Body"/>，不得跨 <c>await</c> 保存，也不得代为释放。
    /// </para>
    /// </summary>
    internal sealed class EncodedBody : IDisposable
    {
        private IDisposable _owner;
        private int _disposed;

        /// <summary>
        /// 以类型码、body 切片与背衬缓冲的持有者创建出站中间体。
        /// </summary>
        internal EncodedBody(int typeCode, ReadOnlyMemory<byte> body, IDisposable owner)
        {
            TypeCode = typeCode;
            Body = body;
            _owner = owner;
        }

        /// <summary>线上 1 字节类型码，由信封的 <see cref="Envelope.Type"/> 拷入。</summary>
        public int TypeCode { get; }

        /// <summary>已编码的 body，不含帧头；只在持有者释放前有效。</summary>
        public ReadOnlyMemory<byte> Body { get; }

        /// <summary>body 背衬缓冲的持有者，由创建者在中间体释放时一并释放。</summary>
        public IDisposable Owner
        {
            get { return _owner; }
        }

        /// <summary>幂等：只释放 <see cref="Owner"/>，重复调用不再重复释放。</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            var owner = Interlocked.Exchange(ref _owner, null);
            if (owner != null)
                owner.Dispose();
        }
    }
}