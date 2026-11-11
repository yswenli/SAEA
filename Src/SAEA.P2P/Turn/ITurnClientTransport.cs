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
*命名空间：SAEA.P2P.Turn
*文件名： ITurnClientTransport
*版本号： v26.11.5.1
*唯一标识：35f8a48f-2e7f-4652-ab42-ba78c7ad02a2
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 03:10:00
*描述：TURN 客户端控制传输契约
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 03:10:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN 客户端控制传输契约
*
*****************************************************************************/
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.P2P.Turn
{
    /// <summary>
    /// TURN 客户端控制传输契约：UDP 上一个数据报就是一帧，TCP/TLS 上由实现内部完成流式分帧后按整帧交付。
    /// 交付给 <see cref="FrameReceived"/> 的始终是完整帧（STUN 报文或整条 ChannelData），调用方不再二次分帧。
    /// </summary>
    public interface ITurnClientTransport : IDisposable
    {
        /// <summary>
        /// 本地绑定的端点；尚未打开或已释放时为 null
        /// </summary>
        IPEndPoint LocalEndPoint { get; }

        /// <summary>
        /// TURN 服务端端点
        /// </summary>
        IPEndPoint ServerEndPoint { get; }

        /// <summary>
        /// 是否为可靠传输：TCP 与 TLS 为 true，UDP 为 false
        /// </summary>
        bool IsReliable { get; }

        /// <summary>
        /// 是否已打开并可收发
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 打开传输：UDP 绑定本地端点并启动接收，TCP/TLS 建立连接并启动读泵
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>打开完成的任务</returns>
        Task OpenAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 发送一整帧；实现内部负责排队与串行化，失败只记日志由上层按超时处理
        /// </summary>
        /// <param name="frame">完整帧字节，不得为 null</param>
        void Send(byte[] frame);

        /// <summary>
        /// 收到一整帧时触发；参数是副本，可在回调返回后继续持有
        /// </summary>
        event Action<byte[]> FrameReceived;
    }
}