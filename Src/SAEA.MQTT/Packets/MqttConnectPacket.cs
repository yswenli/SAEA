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
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MqttConnectPacket数据包类
*
*****************************************************************************/
namespace SAEA.MQTT.Packets
{
    public sealed class MqttConnectPacket : MqttBasePacket
    {
        public string ClientId { get; set; }

        public string Username { get; set; }

        public byte[] Password { get; set; }

        public ushort KeepAlivePeriod { get; set; }

        // Also called "Clean Start" in MQTTv5.
        public bool CleanSession { get; set; }

        public MqttApplicationMessage WillMessage { get; set; }

        #region Added in MQTTv5.0.0

        public MqttConnectPacketProperties Properties { get; set; }

        #endregion

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
