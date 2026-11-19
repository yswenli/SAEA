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
*命名空间：SAEA.MVC.Invocation
*文件名： ActionResolver
*版本号： v26.11.16.1
*唯一标识：093b237e-1b8e-4fa2-ae79-c3d340fb8e44
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 13:48:38
*描述：两段式路由的 action 解析
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 16:12:05
*修改人： yswenli
*版本号： v26.11.16.1
*描述：新增 string method 重载，GET/HEAD 仍只取声明层，其余动词含继承成员
*
*****************************************************************************/
using SAEA.Common;
using SAEA.MVC.Attributes;
using SAEA.MVC.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SAEA.MVC.Invocation
{
    /// <summary>
    /// 两段式路由的 action 解析：按 action 名在控制器类型上匹配方法
    /// </summary>
    internal static class ActionResolver
    {
        /// <summary>
        /// 解析 action 方法：先按 action 名精确匹配；未命中时再尝试 action 名加 Async 后缀回退，
        /// 使两段式地址 /{controller}/{action} 与 WebConfig.DefaultRoute 都能省略 Async 后缀。
        /// 精确名优先，Foo 与 FooAsync 同时声明时 /controller/foo 命中 Foo；回退不限定返回类型。
        /// 属性路由按声明模板匹配，与 action 方法名无关，不走本方法。
        /// WSController 的入口方法声明为 OpenAsync 而地址保持 /chat/open，派生类未 override 时还要
        /// 回落到基类声明的那一层；该回落只对 WSController 派生类生效，否则基类的 OpenAsync、
        /// MessageAsync 等会被误挂到普通控制器上。
        /// GET 与 POST 必须用同一套回退规则，否则 POST /chat/open 会查不到 action 而返回 404
        /// </summary>
        /// <param name="controllerType">控制器类型</param>
        /// <param name="actionName">路由地址里的 action 段原文</param>
        /// <param name="isPost">是否为 POST 请求，决定反射取方法的范围</param>
        /// <returns>匹配到的 action 方法；精确名与 Async 后缀都未命中时返回空列表</returns>
        internal static List<MethodInfo> Resolve(Type controllerType, string actionName, bool isPost)
        {
            return Resolve(controllerType, actionName, isPost ? ConstHelper.POST : ConstHelper.GET);
        }

        /// <summary>
        /// 按请求方法解析 action 方法，回退规则与 <see cref="Resolve(Type, string, bool)"/> 一致，
        /// 区别只在取方法范围由真实动词决定
        /// </summary>
        /// <param name="controllerType">控制器类型</param>
        /// <param name="actionName">路由地址里的 action 段原文</param>
        /// <param name="method">请求方法，为空时按 GET 处理</param>
        /// <returns>匹配到的 action 方法；精确名与 Async 后缀都未命中时返回空列表</returns>
        internal static List<MethodInfo> Resolve(Type controllerType, string actionName, string method)
        {
            if (string.IsNullOrEmpty(method)) method = ConstHelper.GET;

            // GET 与 HEAD 只取声明层、其余动词含继承成员：统一任一方向都会改变 action 的可达面，
            // GET/HEAD 保持原有的读语义可达范围，写动词才放开继承成员
            var flags = HttpMethodCatalog.IsHead(method) || string.Compare(method, ConstHelper.GET, true) == 0
                ? BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly
                : BindingFlags.Instance | BindingFlags.Public;

            var actions = controllerType.GetMethods(flags)
                .Where(b => string.Compare(b.Name, actionName, true) == 0).ToList();

            if (actions.Count > 0) return actions;

            var asyncName = actionName + "Async";

            actions = controllerType.GetMethods(flags)
                .Where(b => string.Compare(b.Name, asyncName, true) == 0).ToList();

            if (actions.Count > 0) return actions;

            // WSController 派生类未 override OpenAsync 时，回落到基类声明的那一层；
            // 该兜底固定取声明层，不受上面的动词范围影响
            if (typeof(WSController).IsAssignableFrom(controllerType))
            {
                actions = typeof(WSController)
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Where(b => string.Compare(b.Name, asyncName, true) == 0).ToList();
            }

            return actions;
        }
    }
}