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
*修改时间：2018/10/28 14:19:50
*修改人： yswenli
*版本号： v26.4.23.1
*描述：Routing接口
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SAEA.MVC
{
    public class Routing
    {
        public Controller Instance
        {
            get; set;
        }

        public string ControllerName
        {
            get; set;
        }

        public MethodInfo Action
        {
            get; set;
        }

        //public FastInvoke.FastInvokeHandler ActionInvoker
        //{
        //    get;set;
        //}

        public string ActionName
        {
            get; set;
        }

        public bool IsPost
        {
            get; set;
        }

        public List<IFilter> FilterAtrrs
        {
            get; set;
        }

        public List<IFilter> ActionFilterAtrrs
        {
            get; set;
        }

        public Dictionary<string, Type> ParmaTypes
        {
            get; set;
        }
    }
}