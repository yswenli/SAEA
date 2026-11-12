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
*命名空间：SAEA.RPC
*文件名： ActionFilterAtrribute
*版本号： v26.4.23.1
*唯一标识：8adc2208-3499-4da1-91dc-743089899665
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/05/25 17:28:26
*描述：ActionFilterAtrribute接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 16:45:50
*修改人： yswenli
*版本号： v26.11.5.1
*描述：过滤器参数由 IUserToken 收敛为连接视图 IChannel
*
*****************************************************************************/
using SAEA.Sockets.Pipeline;
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
        /// <param name="channel">连接通道</param>
        /// <param name="serviceName"></param>
        /// <param name="methodName"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public abstract bool OnActionExecuting(IChannel channel, string serviceName, string methodName, object[] args);

        /// <summary>
        /// 方法执行后
        /// </summary>
        /// <param name="channel">连接通道</param>
        /// <param name="serviceName"></param>
        /// <param name="methodName"></param>
        /// <param name="args"></param>
        /// <param name="result"></param>
        public abstract void OnActionExecuted(IChannel channel, string serviceName, string methodName, object[] args, object result);
    }
}