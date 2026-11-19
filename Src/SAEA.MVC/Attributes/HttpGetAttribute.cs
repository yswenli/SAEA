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
*命名空间：SAEA.MVC.Attributes
*文件名： HttpGetAttribute
*版本号： v26.4.23.1
*唯一标识：17715aa6-8e1a-4440-9794-c79eaa6ee0f8
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/28 14:19:50
*描述：HttpGetAttribute类
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 15:55:14
*修改人： yswenli
*版本号： v26.11.16.1
*描述：改为继承 HttpMethodAttribute，动词与模板统一由基类承载
*
*****************************************************************************/
using SAEA.Common;

using System;

namespace SAEA.MVC.Attributes
{
    /// <summary>
    /// 标记方法为GET
    /// </summary>
    public sealed class HttpGetAttribute : HttpMethodAttribute
    {
        /// <summary>
        /// 不声明路径，仅限定 GET
        /// </summary>
        public HttpGetAttribute() : base(ConstHelper.GET, null)
        {
        }

        /// <summary>
        /// 以路由模板限定 GET
        /// </summary>
        /// <param name="template">路由模板，如 users/{id}</param>
        public HttpGetAttribute(string template) : base(ConstHelper.GET, template)
        {
        }
    }
}