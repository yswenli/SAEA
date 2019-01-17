/****************************************************************************
*项目名称：SAEA.MQTT.Event
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Event
*类 名 称：MqttServerAdapterClientAcceptedEventArgs
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 10:32:04
*描述：
*=====================================================================
*修改时间：2019/1/15 10:32:04
*修 改 人： yswenli
*版 本 号： V3.6.2.2
*描    述：
*****************************************************************************/
using SAEA.MQTT.Interface;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.MQTT.Event
{
    public class MqttServerAdapterClientAcceptedEventArgs : EventArgs
    {
        public MqttServerAdapterClientAcceptedEventArgs(IMqttChannelAdapter client)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public IMqttChannelAdapter Client { get; }

        public Task SessionTask { get; set; }
    }
}
