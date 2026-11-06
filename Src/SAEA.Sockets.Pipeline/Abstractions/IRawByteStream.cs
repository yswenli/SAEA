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
*文件名： IRawByteStream
*版本号： v26.11.5.1
*唯一标识：732dfc89-5bdf-451e-b9ae-c999a3668bb3
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：与传输层解耦的字节流接入面
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：与传输层解耦的字节流接入面
*
*****************************************************************************/
using System;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 与传输层解耦的字节流接入面。核心包不感知具体 socket 类型。
    /// </summary>
    public interface IRawByteStream
    {
        /// <summary>连接标识。</summary>
        string ChannelId { get; }

        /// <summary>
        /// 输入管道。配置接收泵后，该 reader 由泵独占读取；
        /// 同一连接不得再有第二个读者，否则会互相抢数据。
        /// </summary>
        PipeReader Input { get; }

        /// <summary>
        /// 把完整帧交给传输层。<paramref name="owner"/> 在底层发送真正完成或失败后
        /// 由传输层释放；本 Task 表示所有权已交接，不表示对端已收到。
        /// 连接已关闭时必须立即抛 <see cref="ChannelClosedException"/>，不得排队或静默成功。
        /// </summary>
        Task SendAsync(ReadOnlyMemory<byte> data, IDisposable owner);

        /// <summary>同步暂停读取，独立于传输层自身背压。</summary>
        void Pause();

        /// <summary>同步恢复读取。</summary>
        void Resume();

        /// <summary>关闭底层连接，必须幂等。</summary>
        Task CloseAsync(string reason);
    }
}