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
 *文件名： OnServerReceiveSpanHandler
 *版本号： v26.4.23.1
 *唯一标识：f1a8b5c6-3d7e-4a0b-2c65-4e9f0a1b2c3d
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/09/20 17:07:21
 *描述：服务端接收数据委托（Span版本）
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/09/20 17:07:21
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：服务端接收数据委托（Span版本）
 *
 *****************************************************************************/
using System;
using SAEA.Sockets.Interface;

namespace SAEA.Sockets.Handler
{
    /// <summary>
    /// 服务端接收数据（Span 版本）。data 仅在回调期间有效。
    /// </summary>
    public delegate void OnServerReceiveSpanHandler(IUserToken userToken, ReadOnlySpan<byte> data);
}