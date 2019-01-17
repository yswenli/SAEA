/****************************************************************************
*项目名称：SAEA.MQTT.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Model
*类 名 称：ManagedMqttApplicationMessage
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/14 19:08:46
*描述：
*=====================================================================
*修改时间：2019/1/14 19:08:46
*修 改 人： yswenli
*版 本 号： V3.6.2.2
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.MQTT.Model
{
    public class ManagedMqttApplicationMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public MqttApplicationMessage ApplicationMessage { get; set; }
    }
}
