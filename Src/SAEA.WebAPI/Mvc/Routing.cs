/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.WebAPI.Mvc
*文件名： Routing
*版本号： V1.0.0.0
*唯一标识：d48d62ba-6e2b-4eeb-875b-fda8d962f3e0
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/12 10:57:24
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/12 10:57:24
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.Common;
using System.Collections.Generic;
using System.Reflection;

namespace SAEA.WebAPI.Mvc
{
    public class Routing
    {
        public APIController Instance
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

        public FastInvoke.FastInvokeHandler ActionInvoker
        {
            get;set;
        }

        public string ActionName
        {
            get; set;
        }

        public bool IsPost
        {
            get; set;
        }

        public List<object> FilterAtrrs
        {
            get; set;
        }

        public List<object> ActionFilterAtrrs
        {
            get; set;
        }


        public Dictionary<string, object> ParmaTypes
        {
            get; set;
        }
    }
}
