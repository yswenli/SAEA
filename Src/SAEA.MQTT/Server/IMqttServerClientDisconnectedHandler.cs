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
*文件名： IMqttServerClientDisconnectedHandler
*版本号： v26.4.23.1
*唯一标识：d439fabd-678a-4e83-a8a4-135713f07500
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttServerClientDisconnectedHandler接口
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
    /// 客户端断开连接后的回调处理器
    /// </summary>
    public interface IMqttServerClientDisconnectedHandler
    {
        /// <summary>
        /// 处理客户端断开事件，连接对象销毁前调用
        /// </summary>
        /// <param name="eventArgs">事件参数，包含客户端标识与断开类型</param>
        /// <returns>回调完成的任务</returns>
        Task HandleClientDisconnectedAsync(MqttServerClientDisconnectedEventArgs eventArgs);
    }
}
