/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.MVC
*文件名： AreaCollection
*版本号： V3.3.3.5
*唯一标识：eb956356-8ea4-4657-aec1-458a3654c078
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/10 18:10:16
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/10 18:10:16
*修改人： yswenli
*版本号： V3.3.3.5
*描述：
*
*****************************************************************************/
using SAEA.Common;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace SAEA.MVC
{
    /// <summary>
    /// 区域注册类
    /// </summary>
    internal class AreaCollection
    {
        public RouteTable RouteTable { get; set; } = new RouteTable();

        /// <summary>
        /// 记录用户自定义的Controller
        /// </summary>
        public void RegistAll()
        {
            if (RouteTable.Types.Count() < 1)
            {
                StackTrace ss = new StackTrace(true);
                MethodBase mb = ss.GetFrames().Last().GetMethod();
                var space = mb.DeclaringType.Namespace;
                var tt = mb.DeclaringType.Assembly.GetTypes().Where(b => b.FullName.Contains(ConstHelper.CONTROLLERSPACE)).ToList();
                if (tt == null) throw new Exception("当前项目中找不到Controllers空间或命名不符合SAEA.MVC命名规范！");
                RouteTable.Types.AddRange(tt);
            }
        }
        /// <summary>
        /// 加载用户自定义分离的controller
        /// </summary>
        /// <param name="controllerSpaceName"></param>
        public void RegistAll(string controllerSpaceName)
        {
            var fileName = controllerSpaceName + ".dll";
            var assembly = Assembly.LoadFile(PathHelper.GetFullName(fileName));
            var tt = assembly.GetTypes().Where(b => b.FullName.Contains(ConstHelper.CONTROLLERSPACE)).ToList();
            if (tt == null) throw new Exception("当前项目中找不到Controllers空间或命名不符合SAEA.MVC命名规范！");
            RouteTable.Types.AddRange(tt);
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
