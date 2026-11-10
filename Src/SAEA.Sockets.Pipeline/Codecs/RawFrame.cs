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
*文件名： RawFrame
*版本号： v26.11.5.1
*唯一标识：cc9b911b-9068-46da-bff9-eb97ee21d172
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/9 11:24:35
*描述：业务编码器直接产出的原始帧，携带完整字节与背衬缓冲持有者
*
*=====================================================================
*修改标记
*修改时间：2026/11/9 11:24:35
*修改人： yswenli
*版本号： v26.11.5.1
*描述：业务编码器直接产出的原始帧，携带完整字节与背衬缓冲持有者
*
*****************************************************************************/
using System;
using System.Threading;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 业务编码器直接产出的原始帧：完整帧字节加上背衬缓冲的持有者。
    /// 所有权在链头成功移交给传输层后归传输层；移交前可由出站边界或超时补偿释放。
    /// </summary>
    public sealed class RawFrame : IDisposable
    {
        private IDisposable _owner;
        private int _handedOff;
        private int _disposed;

        /// <summary>
        /// 以完整帧字节与背衬缓冲持有者创建原始帧。
        /// </summary>
        /// <param name="bytes">完整帧字节，背衬内存须在移交传输层前保持有效。</param>
        /// <param name="owner">随帧移交的背衬缓冲持有者；没有可归还缓冲时为 null。</param>
        public RawFrame(ReadOnlyMemory<byte> bytes, IDisposable owner)
        {
            Bytes = bytes;
            _owner = owner;
        }

        /// <summary>完整帧字节（含帧头）。</summary>
        public ReadOnlyMemory<byte> Bytes { get; }

        /// <summary>背衬缓冲的持有者；链头成功移交给传输层后由传输层释放。</summary>
        public IDisposable Owner
        {
            get { return _owner; }
        }

        /// <summary>
        /// 把本帧移交给传输层。只有第一次调用成功；已释放或已移交时返回 false。
        /// 成功之后 <see cref="Dispose"/> 不再释放 <see cref="Owner"/>。
        /// </summary>
        /// <returns>本次调用是否取得移交权。</returns>
        internal bool TryMarkHandedOff()
        {
            if (Interlocked.CompareExchange(ref _handedOff, 1, 0) != 0)
                return false;

            // 与 Dispose 的两段检查配合：本调用已置移交位时，若对侧已开始释放则放弃移交，
            // 保证所有者不会被既释放又交给传输层。
            if (Volatile.Read(ref _disposed) != 0)
                return false;

            return true;
        }

        /// <summary>
        /// 幂等释放背衬缓冲持有者。已移交给传输层后不再释放，避免把仍在发送的缓冲提前归还。
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            if (Volatile.Read(ref _handedOff) != 0)
                return;

            var owner = Interlocked.Exchange(ref _owner, null);
            if (owner != null)
                owner.Dispose();
        }
    }
}