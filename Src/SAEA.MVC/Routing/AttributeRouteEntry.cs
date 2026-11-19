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
*命名空间：SAEA.MVC.Routing
*文件名： AttributeRouteEntry
*版本号： v26.11.16.1
*唯一标识：c429b31c-505b-4c88-85a9-f5dce502be45
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 14:27:41
*描述：属性路由条目：控制器、action、模板、段与动词声明
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 16:15:31
*修改人： yswenli
*版本号： v26.11.16.1
*描述：AllowsMethod 把 OPTIONS 限定为仅命中显式声明 [HttpOptions] 的条目，未声明动词的条目不再吞掉 OPTIONS
*
*****************************************************************************/
using SAEA.Common;
using SAEA.MVC.Attributes;

using System;

namespace SAEA.MVC.Routing
{
    /// <summary>
    /// 一个 action 的属性路由条目
    /// </summary>
    internal sealed class AttributeRouteEntry
    {
        internal Type ControllerType { get; set; }

        internal string ActionName { get; set; }

        internal string Template { get; set; }

        /// <summary>
        /// 路径形状：参数段归一化为 {}，静态段保留原文
        /// </summary>
        internal string Shape { get; set; }

        internal Segment[] Segments { get; set; }

        internal string[] HttpMethods { get; set; }

        internal int Order { get; set; }

        internal int StaticSegmentCount { get; set; }

        internal bool HasParameters { get; set; }

        internal bool AllowsMethod(string method)
        {
            // OPTIONS 是协议级能力询问，未显式声明时由 RequestPipeline 回 204 + Allow，
            // 不能让只带 [Route] 的普通 action 被 OPTIONS 触发
            if (HttpMethodCatalog.IsOptions(method)) return DeclaresVerb(ConstHelper.OPTIONS);

            var methods = HttpMethods;

            // 未声明动词的条目对除 OPTIONS 外的任何方法放行
            if (methods == null) return true;

            for (var i = 0; i < methods.Length; i++)
            {
                if (string.Compare(methods[i], method, true) == 0) return true;
            }

            // HEAD 复用 GET：只声明 GET 的条目也要响应 HEAD 探测，报文体由响应层丢弃
            if (HttpMethodCatalog.IsHead(method)) return DeclaresVerb(ConstHelper.HEAD) || DeclaresVerb(ConstHelper.GET);

            return false;
        }

        internal bool DeclaresVerb(string verb)
        {
            var methods = HttpMethods;

            if (methods == null) return false;

            for (var i = 0; i < methods.Length; i++)
            {
                if (string.Compare(methods[i], verb, true) == 0) return true;
            }

            return false;
        }

        /// <summary>
        /// 一个模板段：占位段携带参数名，静态段携带原文
        /// </summary>
        internal sealed class Segment
        {
            internal bool IsParameter { get; set; }

            internal string Value { get; set; }
        }
    }
}