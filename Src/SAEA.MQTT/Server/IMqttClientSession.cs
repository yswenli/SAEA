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
*文件名： IMqttClientSession
*版本号： v26.4.23.1
*唯一标识：8782ed85-c09a-45b0-a81e-0802e4f19495
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttClientSession接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System.Threading.Tasks;

namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 服务端维护的单个客户端会话，负责该客户端的订阅表和待投递消息队列
    /// </summary>
    public interface IMqttClientSession
    {
        /// <summary>
        /// 客户端标识，由 CONNECT 报文提供或服务端分配
        /// </summary>
        string ClientId { get; }

        /// <summary>
        /// 停止会话并释放其占用的订阅与消息队列资源
        /// </summary>
        /// <returns>会话停止完成的任务</returns>
        Task StopAsync();
    }
}