/****************************************************************************
*项目名称：SAEA.MQTT.Common.Serializer
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.MQTT.Common.Serializer
*类 名 称：MqttFixedHeader
*版 本 号： v4.3.2.5
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/1/15 15:33:37
*描述：
*=====================================================================
*修改时间：2019/1/15 15:33:37
*修 改 人： yswenli
*版 本 号： v4.3.2.5
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.MQTT.Common.Serializer
{
    public struct MqttFixedHeader
    {
        public MqttFixedHeader(byte flags, int remainingLength)
        {
            Flags = flags;
            RemainingLength = remainingLength;
        }

        public byte Flags { get; }

        public int RemainingLength { get; }
    }
}
