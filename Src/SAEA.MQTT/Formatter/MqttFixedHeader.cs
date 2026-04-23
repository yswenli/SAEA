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
*文件名： MqttFixedHeader
*版本号： v26.4.23.1
*唯一标识：427c1892-15a0-4802-88d3-22028d83dc0e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
namespace SAEA.MQTT.Formatter
{
    public struct MqttFixedHeader
    {
        public MqttFixedHeader(byte flags, int remainingLength, int totalLength)
        {
            Flags = flags;
            RemainingLength = remainingLength;
            TotalLength = totalLength;
        }

        public byte Flags { get; }

        public int RemainingLength { get; }

        public int TotalLength { get; }
    }
}
