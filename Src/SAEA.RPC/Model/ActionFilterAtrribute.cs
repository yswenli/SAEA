﻿/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RPC.Model
*文件名： ActionFilterAtrribute
*版本号： v7.0.0.1
*唯一标识：dd3818b2-de6c-414e-8375-8a9e0f0fec1f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/17 14:22:22
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/17 14:22:22
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Sockets.Interface;
using System;

namespace SAEA.RPC
{
    /// <summary>
    /// SAEA.RPC AOP入口类
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public abstract class ActionFilterAtrribute : Attribute
    {
        /// <summary>
        /// 执行顺序
        /// </summary>
        public int Order
        {
            get; set;
        }

        /// <summary>
        /// 方法执行前
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="serviceInfo"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public abstract bool OnActionExecuting(IUserToken userToken, string serviceName, string methodName, object[] args);

        /// <summary>
        /// 方法执行后
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="serviceName"></param>
        /// <param name="methodName"></param>
        /// <param name="args"></param>
        /// <param name="result"></param>
        public abstract void OnActionExecuted(IUserToken userToken, string serviceName, string methodName, object[] args, object result);
    }
}
