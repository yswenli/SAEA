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
*命名空间：SAEA.P2P.Transport
*文件名： ITransport
*版本号： v26.11.5.1
*唯一标识：3e7ebbaa-8518-457f-9697-b4ed76ef8fd0
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 12:10:00
*描述：传输层最小契约
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 12:10:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增传输层最小契约
*
*****************************************************************************/
using System;
using System.Net;

namespace SAEA.P2P.Transport
{
    /// <summary>
    /// 传输层最小契约：本地端点、可用状态与释放。
    /// ICE 与 TURN 只依赖该契约，不直接依赖 SAEA.Sockets，测试可用用户态 NAT 代理替换实现。
    /// </summary>
    public interface ITransport : IDisposable
    {
        /// <summary>
        /// 本地绑定的端点；尚未绑定或已释放时为 null
        /// </summary>
        IPEndPoint LocalEndPoint { get; }

        /// <summary>
        /// 是否可用于收发：UDP 表示已绑定，TCP 连接侧表示已建链、监听侧表示正在监听
        /// </summary>
        bool IsConnected { get; }
    }
}