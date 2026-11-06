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
*命名空间：SAEA.MQTT.Packets
*文件名： MqttConnectPacket
*版本号： v26.4.23.1
*唯一标识：a3934b8c-0437-4acc-a16b-98792ebc242c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttConnectPacket数据包类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.MQTT.Packets
{
    /// <summary>
    /// CONNECT 报文，客户端发起连接时发送
    /// </summary>
    public sealed class MqttConnectPacket : MqttBasePacket
    {
        /// <summary>
        /// 客户端标识，MQTT 3.1.1 起允许为空由服务端分配
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// 用户名，null 表示不使用用户名
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// 密码原始字节，null 表示不使用密码
        /// </summary>
        public byte[] Password { get; set; }

        /// <summary>
        /// 保活间隔，单位秒，0 表示关闭保活
        /// </summary>
        public ushort KeepAlivePeriod { get; set; }

        /// <summary>
        /// 是否清理会话，MQTT 5.0 中对应 Clean Start
        /// </summary>
        // Also called "Clean Start" in MQTTv5.
        public bool CleanSession { get; set; }

        /// <summary>
        /// 遗嘱消息，null 表示不设置遗嘱，MQTT 3.1.1 起支持
        /// </summary>
        public MqttApplicationMessage WillMessage { get; set; }

        #region Added in MQTTv5.0.0

        /// <summary>
        /// 连接属性，仅 MQTT 5.0 使用
        /// </summary>
        public MqttConnectPacketProperties Properties { get; set; }

        #endregion

        /// <summary>
        /// 返回连接参数描述文本，密码仅以掩码形式出现
        /// </summary>
        /// <returns>报文描述文本</returns>
        public override string ToString()
        {
            var passwordText = string.Empty;

            if (Password != null)
            {
                passwordText = "****";
            }

            return string.Concat("Connect: [ClientId=", ClientId, "] [Username=", Username, "] [Password=", passwordText, "] [KeepAlivePeriod=", KeepAlivePeriod, "] [CleanSession=", CleanSession, "]");
        }
    }
}
