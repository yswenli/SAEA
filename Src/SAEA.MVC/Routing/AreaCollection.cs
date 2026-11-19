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
*命名空间：SAEA.MVC.Routing
*文件名： AreaCollection
*版本号： v26.4.23.1
*唯一标识：47c089e9-15e1-49c5-b468-00a0ba115070
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/28 14:19:50
*描述：AreaCollection接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 11:16:00
*修改人： yswenli
*版本号： v26.11.16.1
*描述：文件移入 Routing/ 并改用命名空间 SAEA.MVC.Routing，无逻辑变更
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace SAEA.MVC.Routing
{
    /// <summary>
    /// 区域注册类
    /// </summary>
    internal class AreaCollection
    {
        public RouteTable RouteTable { get; set; } = new RouteTable();

        /// <summary>
        /// 本次 RegistAll 发现的控制器类型，供属性路由构建使用
        /// </summary>
        public List<Type> DiscoveredTypes { get; private set; } = new List<Type>();

        /// <summary>
        /// 记录用户自定义的Controller
        /// </summary>
        public void RegistAll()
        {
            StackTrace ss = new StackTrace(true);

            List<Type> list = new List<Type>();

            foreach (var item in ss.GetFrames())
            {
                list.AddRange(item.GetMethod().DeclaringType.Assembly.GetTypes());
            }

            var tt = list.Where(IsControllerType).ToList();

            if (tt == null || !tt.Any())
            {
                LogHelper.Error("AreaCollection.RegistAll出现异常", new Exception("找不到符合MVC命名的Controllers空间!"));
            }

            RouteTable.Types.AddRange(tt);
            RouteTable.Types = RouteTable.Types.Distinct().ToList();

            // 属性路由只按本次发现的类型构建，静态 Types 会跨应用累加并误报冲突
            DiscoveredTypes = tt.Distinct().ToList();
        }

        /// <summary>
        /// 加载用户自定义分离的controller
        /// </summary>
        /// <param name="controllerSpaceName"></param>
        public void RegistAll(string controllerSpaceName)
        {
            List<Type> tt = null;

            try
            {
                var fileName = controllerSpaceName + ".dll";
                var assembly = Assembly.LoadFile(PathHelper.GetFullName(fileName));
                tt = assembly.GetTypes().Where(IsControllerType).ToList();
            }
            catch
            {

            }

            List<Type> tt2 = null;
            try
            {
                var fileName2 = controllerSpaceName + ".exe";
                var assembly2 = Assembly.LoadFile(PathHelper.GetFullName(fileName2));
                tt2 = assembly2.GetTypes().Where(IsControllerType).ToList();
            }
            catch { }

            if (tt == null && tt2 == null) throw new Exception("当前项目中找不到Controllers空间或命名不符合SAEA.MVC命名规范！");

            if (tt != null)
                RouteTable.Types.AddRange(tt);
            if (tt2 != null)
                RouteTable.Types.AddRange(tt2);

            RouteTable.Types = RouteTable.Types.Distinct().ToList();

            var discovered = new List<Type>();

            if (tt != null) discovered.AddRange(tt);
            if (tt2 != null) discovered.AddRange(tt2);

            DiscoveredTypes = discovered.Distinct().ToList();
        }


        /// <summary>
        /// 是否是控制器类型：基类链上有 Controller，且自身不是抽象类
        /// （WSController 是抽象类，不会被当成可路由的控制器）
        /// </summary>
        static bool IsControllerType(Type type)
        {
            return type != null && !type.IsAbstract && typeof(Controller).IsAssignableFrom(type);
        }

        /// <summary>
        /// 是否存在此控制器
        /// </summary>
        /// <param name="controllerName"></param>
        /// <returns></returns>
        public bool Exists(string controllerName)
        {            
            return RouteTable.Types.Exists(b => string.Compare(b.Name, controllerName, true) == 0 || string.Compare(b.Name, controllerName + ConstHelper.CONTROLLERNAME, true) == 0);
        }
    }
}