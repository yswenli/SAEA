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
*文件名： TransportFrame
*版本号： v26.11.5.1
*唯一标识：77bd9008-e83b-4acd-b75e-6a5363d3e79d
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：出站终态：完整帧字节加上背衬缓冲的归还有效载体
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：出站终态：完整帧字节加上背衬缓冲的归还有效载体
*
*****************************************************************************/
using System;
using System.Threading;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 出站终态：完整帧字节加上背衬缓冲的归还有效载体。
    /// 所有权在 <see cref="IRawByteStream.SendAsync"/> 正常返回后交给传输层，链头不再释放 <see cref="Owner"/>。
    /// </summary>
    internal sealed class TransportFrame : IDisposable
    {
        private IDisposable _owner;
        private int _disposed;

        /// <summary>
        /// 以完整帧字节与其背衬缓冲的持有者创建出站终态。
        /// <paramref name="owner"/> 允许为 null（帧字节不来自池时没有可归还的缓冲）。
        /// </summary>
        internal TransportFrame(ReadOnlyMemory<byte> data, IDisposable owner)
        {
            Data = data;
            _owner = owner;
        }

        /// <summary>完整帧字节（含帧头）。</summary>
        public ReadOnlyMemory<byte> Data { get; }

        /// <summary>背衬缓冲的持有者，发送完成后由传输层释放；没有可归还缓冲时为 null。</summary>
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