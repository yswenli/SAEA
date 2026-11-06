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
*文件名： MqttClientDisconnectType
*版本号： v26.4.23.1
*唯一标识：f1ac1e33-8e9d-417a-b9b2-9614662dba87
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttClientDisconnectType接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.MQTT.Server
{
    /// <summary>
    /// 客户端连接的断开方式，决定会话保留与遗嘱消息的处理
    /// </summary>
    public enum MqttClientDisconnectType
    {
        /// <summary>
        /// 收到客户端 DISCONNECT 报文后正常断开
        /// </summary>
        Clean,
        /// <summary>
        /// 未收到 DISCONNECT 即断开，含网络错误、协议错误与保活超时
        /// </summary>
        NotClean,
        /// <summary>
        /// 同一 ClientId 的新连接接管，旧连接被服务端主动关闭
        /// </summary>
        Takeover
    }
}
