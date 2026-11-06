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
*命名空间：SAEA.MQTT.Extensions.ManagedClient
*文件名： ManagedProcessFailedEventArgs
*版本号： v26.4.23.1
*唯一标识：9eceb61c-0fca-4e2a-be2f-d2fd3207cdf8
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：ManagedProcessFailedEventArgs接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;

namespace SAEA.MQTT.Extensions.ManagedClient
{
    /// <summary>
    /// 托管客户端后台处理失败的事件参数
    /// </summary>
    public class ManagedProcessFailedEventArgs : EventArgs
    {
        /// <summary>
        /// 使用失败异常初始化实例
        /// </summary>
        /// <param name="exception">失败异常，不能为null</param>
        public ManagedProcessFailedEventArgs(Exception exception)
        {
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        /// <summary>
        /// 引起处理失败的异常
        /// </summary>
        public Exception Exception { get; }
    }
}