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
*命名空间：SAEA.MVC.Attributes
*文件名： WebSocketAttribute
*版本号： v26.11.5.1
*唯一标识：be8234e4-406b-47a1-9ae2-c7fa3d07048a
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/5 11:56:30
*描述：标记一个 action 为 WebSocket 入口
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 15:26:46
*修改人： yswenli
*版本号： v26.11.16.1
*描述：命名空间改为与目录一致（SAEA.MVC.Attributes/Controllers/Hosting/Results）
*
*****************************************************************************/
using System;

namespace SAEA.MVC.Attributes
{
    /// <summary>
    /// 标记一个 action 为 WebSocket 入口。
    /// 只有挂了这个特性的 action 才允许 AcceptWebSocketAsync，其它 action 直连返回 400。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class WebSocketAttribute : Attribute
    {
    }
}