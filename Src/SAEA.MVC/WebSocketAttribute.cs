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
 *命名空间：SAEA.MVC
 *文件名： WebSocketAttribute
 *描述：标记一个 action 为 WebSocket 入口
 *
 *****************************************************************************/
using System;

namespace SAEA.MVC
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