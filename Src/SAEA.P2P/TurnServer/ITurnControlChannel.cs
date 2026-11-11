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
*命名空间：SAEA.P2P.TurnServer
*文件名： ITurnControlChannel
*版本号： v26.11.5.1
*唯一标识：fe055e0a-8065-4e2b-8e96-be5acd6762cd
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 01:30:00
*描述：TURN 控制连接抽象
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 03:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 IsTls 与 DataStream，为连接接管为数据连接预留
*
*****************************************************************************/
using System.IO;
using System.Net;

namespace SAEA.P2P.TurnServer
{
    /// <summary>
    /// TURN 控制连接抽象：UDP 与 TCP/TLS 控制连接共用同一套分配、权限、通道与中继逻辑，
    /// 差异只在回送方式与 ChannelData 是否需要补齐。
    /// </summary>
    internal interface ITurnControlChannel
    {
        /// <summary>
        /// 客户端端点；UDP 下为数据报来源，TCP 下为连接的对端
        /// </summary>
        IPEndPoint RemoteEndPoint { get; }

        /// <summary>
        /// 控制连接的本地端点，参与分配表的隔离键
        /// </summary>
        IPEndPoint LocalEndPoint { get; }

        /// <summary>
        /// 是否流式连接；为 true 时 ChannelData 必须补齐到 4 字节倍数
        /// </summary>
        bool IsStream { get; }

        /// <summary>
        /// 是否为 TLS 连接；数据连接必须与申请分配的控制连接保持同一加密层
        /// </summary>
        bool IsTls { get; }

        /// <summary>
        /// 接管为数据连接时使用的双向字节流；UDP 控制连接没有流，返回 null
        /// </summary>
        Stream DataStream { get; }

        /// <summary>
        /// 把一条完整报文写回这条控制连接
        /// </summary>
        /// <param name="data">待发送的完整报文</param>
        void Send(byte[] data);
    }
}