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
*文件名： ReconnectionResult
*版本号： v26.4.23.1
*唯一标识：9f4b1cfa-eb3a-4739-b4c0-c397ac40399e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：ReconnectionResult接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.MQTT.Extensions.ManagedClient
{
    /// <summary>
    /// 托管客户端连接维护的结果
    /// </summary>
    public enum ReconnectionResult
    {
        /// <summary>
        /// 连接仍然可用，无需重连
        /// </summary>
        StillConnected,
        /// <summary>
        /// 重连成功但服务端未保留原会话，需重新下发订阅
        /// </summary>
        Reconnected,
        /// <summary>
        /// 重连成功且服务端保留了原会话，订阅无需重发
        /// </summary>
        Recovered,
        /// <summary>
        /// 重连失败，当前未连接
        /// </summary>
        NotConnected
    }
}
