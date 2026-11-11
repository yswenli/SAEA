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
*命名空间：SAEA.Sockets.Handler
*文件名： OnClientReceiveSpanFromHandler
*版本号： v26.11.5.1
*唯一标识：abf5b47c-2f86-41f3-9dbd-883c6f58e07c
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 11:00:00
*描述：UDP 客户端带来源地址的接收数据委托（Span 版本）
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 11:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增带来源地址的接收回调
*
*****************************************************************************/
using System;
using System.Net;

namespace SAEA.Sockets.Handler
{
    /// <summary>
    /// UDP 客户端接收数据（Span 版本），同时给出该数据报的来源地址。
    /// 地址对象与本端数据都只在回调期间有效，跨回调保存必须复制。
    /// </summary>
    /// <param name="remoteEndPoint">数据报来源端点，回调返回后可能被底层复用，跨回调保存必须复制</param>
    /// <param name="data">收到的数据，仅在回调期间有效，跨回调保存必须复制</param>
    public delegate void OnClientReceiveSpanFromHandler(IPEndPoint remoteEndPoint, ReadOnlySpan<byte> data);
}