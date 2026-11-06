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
*文件名： ApplicationMessageProcessedEventArgs
*版本号： v26.4.23.1
*唯一标识：66a5ad7e-f794-428f-99cd-ba66fc2a5b3f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：ApplicationMessageProcessedEventArgs接口
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
    /// 应用消息处理完成事件参数
    /// </summary>
    public class ApplicationMessageProcessedEventArgs : EventArgs
    {
        /// <summary>
        /// 使用已处理的消息与结果异常初始化实例
        /// </summary>
        /// <param name="applicationMessage">已处理的应用消息，不能为null</param>
        /// <param name="exception">处理失败时的异常，成功时为null</param>
        public ApplicationMessageProcessedEventArgs(ManagedMqttApplicationMessage applicationMessage, Exception exception)
        {
            ApplicationMessage = applicationMessage ?? throw new ArgumentNullException(nameof(applicationMessage));
            Exception = exception;
        }

        /// <summary>
        /// 已处理的应用消息
        /// </summary>
        public ManagedMqttApplicationMessage ApplicationMessage { get; }
        /// <summary>
        /// 处理失败时的异常，成功时为null
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// 处理是否失败
        /// </summary>
        public bool HasFailed => Exception != null;
        /// <summary>
        /// 处理是否成功
        /// </summary>
        public bool HasSucceeded => Exception == null;
    }
}
