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
*命名空间：SAEA.MQTT.Server
*文件名： IMqttServerStartedHandler
*版本号： v26.4.23.1
*唯一标识：5aee1a11-cc3f-4ae9-bcbf-0a4d1b2dd50b
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttServerStartedHandler处理类
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
using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 服务端启动完成后的回调处理器
    /// </summary>
    public interface IMqttServerStartedHandler
    {
        /// <summary>
        /// 处理服务端启动完成事件，所有端点已开始监听
        /// </summary>
        /// <param name="eventArgs">事件参数，当前无附加信息</param>
        /// <returns>回调完成的任务</returns>
        Task HandleServerStartedAsync(EventArgs eventArgs);
    }
}
