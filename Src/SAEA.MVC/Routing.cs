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
*文件名： Routing
*版本号： v26.4.23.1
*唯一标识：be6076b1-1b68-42cb-a986-f2aec030d744
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/28 14:19:50
*描述：Routing接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/17 22:00:00
*修改人： yswenli
*版本号： v26.11.16.1
*描述：HttpMethods 注释改为限定动词，两段式 action 也可带限定动词
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SAEA.MVC
{
    /// <summary>
    /// 单条控制器 action 的路由信息与过滤器缓存
    /// </summary>
    public class Routing
    {
        /// <summary>
        /// 控制器实例
        /// </summary>
        public Controller Instance
        {
            get; set;
        }

        /// <summary>
        /// 控制器类型名
        /// </summary>
        public string ControllerName
        {
            get; set;
        }

        /// <summary>
        /// 目标 action 方法
        /// </summary>
        public MethodInfo Action
        {
            get; set;
        }

        //public FastInvoke.FastInvokeHandler ActionInvoker
        //{
        //    get;set;
        //}

        /// <summary>
        /// 路由中配置的 action 名
        /// </summary>
        public string ActionName
        {
            get; set;
        }

        /// <summary>
        /// 是否为 POST 路由
        /// </summary>
        public bool IsPost
        {
            get; set;
        }

        /// <summary>
        /// 控制器类上声明的过滤器，已按 Order 升序
        /// </summary>
        public List<IFilter> FilterAtrrs
        {
            get; set;
        }

        /// <summary>
        /// action 方法上声明的过滤器，已按 Order 升序
        /// </summary>
        public List<IFilter> ActionFilterAtrrs
        {
            get; set;
        }

        /// <summary>
        /// action 参数名到参数类型的映射
        /// </summary>
        public Dictionary<string, Type> ParmaTypes
        {
            get; set;
        }

        /// <summary>
        /// 归一化后的属性路由模板，含 {name} 占位符；两段式路由为 null
        /// </summary>
        public string Template
        {
            get; set;
        }

        /// <summary>
        /// 限定动词；null 表示不限动词
        /// </summary>
        public string[] HttpMethods
        {
            get; set;
        }
    }
}