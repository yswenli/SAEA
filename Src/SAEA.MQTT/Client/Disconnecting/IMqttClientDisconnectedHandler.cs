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
*命名空间：SAEA.MQTT.Client.Disconnecting
*文件名： IMqttClientDisconnectedHandler
*版本号： v26.4.23.1
*唯一标识：3b06ec5b-a3db-492a-bdd7-0be3a479c6d1
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttClientDisconnectedHandler接口
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

namespace SAEA.MQTT.Client.Disconnecting
{
    /// <summary>
    /// 客户端连接断开后的回调处理接口
    /// </summary>
    public interface IMqttClientDisconnectedHandler
    {
        /// <summary>
        /// 连接断开后回调
        /// </summary>
        /// <param name="eventArgs">断开事件参数，含是否曾连接成功、异常与断开原因</param>
        /// <returns>回调完成的任务</returns>
        Task HandleDisconnectedAsync(MqttClientDisconnectedEventArgs eventArgs);
    }
}
