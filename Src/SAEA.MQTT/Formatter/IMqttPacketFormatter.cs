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
*命名空间：SAEA.MQTT.Formatter
*文件名： IMqttPacketFormatter
*版本号： v26.4.23.1
*唯一标识：411bbc43-efea-4258-84a0-430279bad6f1
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MQTT数据格式化类
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MQTT数据格式化类
*
*****************************************************************************/
using System;
using SAEA.MQTT.Adapter;
using SAEA.MQTT.Packets;

namespace SAEA.MQTT.Formatter
{
    public interface IMqttPacketFormatter
    {
        IMqttDataConverter DataConverter { get; }

        ArraySegment<byte> Encode(MqttBasePacket mqttPacket);

        MqttBasePacket Decode(ReceivedMqttPacket receivedMqttPacket);

        void FreeBuffer();
    }
}