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
*文件名： HttpMethodAttribute
*版本号： v26.11.16.1
*唯一标识：78ebc388-0467-4201-a565-8d6e46f3f8e0
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 15:55:14
*描述：动词特性基类，统一承载限定的 HTTP 方法与可选路由模板
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 15:55:14
*修改人： yswenli
*版本号： v26.11.16.1
*描述：新增，作为 HttpGet/HttpPost/HttpPut/HttpDelete/HttpPatch/HttpHead/HttpOptions 的公共基类
*
*****************************************************************************/
using System;

namespace SAEA.MVC.Attributes
{
    /// <summary>
    /// 动词特性基类：限定 action 可接受的 HTTP 方法，并可携带路由模板
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public abstract class HttpMethodAttribute : Attribute, IFilter
    {
        /// <summary>
        /// 以指定动词与路由模板初始化
        /// </summary>
        /// <param name="method">HTTP 方法名，如 GET</param>
        /// <param name="template">路由模板，未声明时为 null</param>
        protected HttpMethodAttribute(string method, string template)
        {
            Method = method;
            Template = template;
        }

        /// <summary>
        /// 限定的 HTTP 方法名
        /// </summary>
        public string Method { get; }

        /// <summary>
        /// 声明的路由模板，未声明时为 null
        /// </summary>
        public string Template { get; }

        /// <summary>
        /// 过滤器执行顺序
        /// </summary>
        public int Order { get; set; }
    }
}