/****************************************************************************
*项目名称：SAEA.MQTT.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Model
*类 名 称：MqttPendingMessagesOverflowStrategy
*版 本 号： V4.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/14 19:55:00
*描述：
*=====================================================================
*修改时间：2019/1/14 19:55:00
*修 改 人： yswenli
*版 本 号： V4.0.0.1
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.MQTT.Model
{
    public enum MqttPendingMessagesOverflowStrategy
    {
        DropOldestQueuedMessage,
        DropNewMessage
    }
}
