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
*文件名： ApplicationMessageSkippedEventArgs
*版本号： v26.4.23.1
*唯一标识：aed8789c-be3c-473d-81d9-23733ef122e6
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：ApplicationMessageSkippedEventArgs接口
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
    /// 应用消息被跳过的事件参数
    /// </summary>
    public class ApplicationMessageSkippedEventArgs : EventArgs
    {
        /// <summary>
        /// 使用被跳过的应用消息初始化实例
        /// </summary>
        /// <param name="applicationMessage">被跳过的应用消息，不能为null</param>
        public ApplicationMessageSkippedEventArgs(ManagedMqttApplicationMessage applicationMessage)
        {
            ApplicationMessage = applicationMessage ?? throw new ArgumentNullException(nameof(applicationMessage));
        }

        /// <summary>
        /// 被跳过的应用消息
        /// </summary>
        public ManagedMqttApplicationMessage ApplicationMessage { get; }
    }
}
