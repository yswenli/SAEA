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
 *命名空间：SAEA.Sockets.Handler
 *文件名： OnClientReceiveSpanHandler
 *版本号： v26.4.23.1
 *唯一标识：e0f7a4b5-2c6d-4f9a-1b54-3d8e9f0a1b2c
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/09/20 17:07:21
 *描述：客户端接收数据委托（Span版本）
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/09/20 17:07:21
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：客户端接收数据委托（Span版本）
 *
 *****************************************************************************/
using System;

namespace SAEA.Sockets.Handler
{
    /// <summary>
    /// 客户端接收数据（Span 版本）。data 仅在回调期间有效。
    /// </summary>
    public delegate void OnClientReceiveSpanHandler(ReadOnlySpan<byte> data);
}