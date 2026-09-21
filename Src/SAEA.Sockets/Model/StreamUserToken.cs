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
 *命名空间：SAEA.Sockets.Model
 *文件名： StreamUserToken
 *版本号： v26.4.23.1
 *唯一标识：7f3c2b8a-1d64-4e9b-a5c2-0e6f9b1d4a37
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2019/02/11 17:03:06
 *描述：StreamUserToken
 *
 *=====================================================================
 *修改标记
 *修改时间：2019/02/11 17:03:06
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：StreamUserToken
 *
 *****************************************************************************/
using System.IO;
using System.IO.Pipelines;

namespace SAEA.Sockets.Model
{
    /// <summary>
    /// Stream 通道的用户令牌，承载连接流与其 PipeReader 输入。
    /// </summary>
    /// <remarks>
    /// 本令牌用于 Stream 后端。继承自 <see cref="SAEA.Sockets.Base.BaseUserToken"/> 的 IOCP 专用成员
    /// （<c>ReadArgs</c>/<c>WriteArgs</c>/<c>IsSending</c>/<c>WaitWrite</c>/<c>ReleaseWrite</c>）在 Stream 场景
    /// 不适用，消费方只应依赖 <see cref="SAEA.Sockets.Interface.IUserToken.ID"/>、<see cref="Stream"/>、
    /// <see cref="Input"/>（以及 <see cref="SAEA.Sockets.Interface.IUserToken.Coder"/>）。
    /// <see cref="Stream"/> 与该连接的 <see cref="ChannelInfo.Stream"/> 是同一实例，不得独立重新赋值。
    /// </remarks>
    public class StreamUserToken : SAEA.Sockets.Base.BaseUserToken
    {
        /// <summary>
        /// 获取或设置通道的网络流
        /// </summary>
        public Stream Stream { get; set; }

        /// <summary>
        /// 获取或设置通道的 PipeReader
        /// </summary>
        public PipeReader Input { get; set; }

        /// <summary>
        /// 释放令牌资源。执行顺序固定为：先 <see cref="PipeReader.CancelPendingRead"/>（唤醒正在
        /// <c>ReadAsync</c> 的接收循环），再 <see cref="PipeReader.Complete"/>，最后调用
        /// <see cref="SAEA.Sockets.Base.BaseUserToken.Clear"/> 关闭 <c>Socket</c>。先取消后完成可避免挂起的读取
        /// 因读取器被释放而抛出 <see cref="System.ObjectDisposedException"/>；完成与取消均幂等，可安全重复调用。
        /// </summary>
        /// <remarks>
        /// <see cref="Input"/> 由 <see cref="Core.Tcp.StreamServerSocket"/> 以
        /// <c>new StreamPipeReaderOptions(leaveOpen: true)</c> 创建，故 <see cref="PipeReader.Complete"/> 不会释放
        /// <see cref="Stream"/>（网络流归 <see cref="ChannelInfo.Stream"/> 所有，此处仅置空引用）。
        /// <see cref="SAEA.Sockets.Base.BaseUserToken.Clear"/> 未标记为 <c>virtual</c>，故此处以 <c>new</c>
        /// 隐藏而非重写。经 <see cref="Interface.IUserToken"/> 接口调用仍会落到基类实现；库内唯一调用点
        /// <see cref="Core.Tcp.StreamServerSocket.Stop"/> 使用具体类型 <see cref="StreamUserToken"/> 调用，
        /// 会命中本方法。
        /// </remarks>
        public new void Clear()
        {
            var reader = Input;
            Input = null;

            if (reader != null)
            {
                try { reader.CancelPendingRead(); } catch { }
                try { reader.Complete(); } catch { }
            }

            Stream = null;

            base.Clear();
        }
    }
}
