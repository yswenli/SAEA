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
*命名空间：SAEA.RPC.Model
*文件名： ServiceInfo
*版本号： v26.4.23.1
*唯一标识：bff39737-6f3a-40db-91ad-99a1679de735
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/05/25 17:28:26
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/05/25 17:28:26
*修改人： yswenli
*版本号： v26.4.23.1
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