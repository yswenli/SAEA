/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RPC.Model
*文件名： ServiceInfo
*版本号： v4.5.6.7
*唯一标识：920c4d8f-440c-4b41-80ad-5db737236bb1
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/16 17:47:54
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/16 17:47:54
*修改人： yswenli
*版本号： v4.5.6.7
*描述：
*
*****************************************************************************/
using SAEA.Common;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace SAEA.RPC.Model
{
    /// <summary>
    /// 服务类信息
    /// </summary>
    internal class ServiceInfo
    {
        public Type Type
        {
            get; set;
        }

        public MethodInfo Method
        {
            get; set;
        }

        public object Instance
        {
            get; set;
        }

        public List<ActionFilterAtrribute> FilterAtrrs { get; set; } = new List<ActionFilterAtrribute>();

        public List<ActionFilterAtrribute> ActionFilterAtrrs { get; set; } = new List<ActionFilterAtrribute>();

        public Dictionary<string, Type> Pamars
        {
            get; set;
        } = new Dictionary<string, Type>();


    }
}
