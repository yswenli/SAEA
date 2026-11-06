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
*命名空间：SAEA.MQTT.Adapter
*文件名： ReceivedMqttPacket
*版本号： v26.4.23.1
*唯一标识：8ce96a58-f5be-485e-9841-e6c89176f318
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：ReceivedMqttPacket接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.MQTT.Formatter;

namespace SAEA.MQTT.Adapter
{
    /// <summary>
    /// 已接收但尚未解码的原始 MQTT 报文
    /// </summary>
    public sealed class ReceivedMqttPacket
    {
        /// <summary>
        /// 以固定头、报文体读取器和总长度构造
        /// </summary>
        /// <param name="fixedHeader">固定头首字节，高 4 位为报文类型、低 4 位为标志位</param>
        /// <param name="body">报文体读取器，无报文体时为 null</param>
        /// <param name="totalLength">固定头与报文体合计的字节数</param>
        public ReceivedMqttPacket(byte fixedHeader, IMqttPacketBodyReader body, int totalLength)
        {
            FixedHeader = fixedHeader;
            Body = body;
            TotalLength = totalLength;
        }

        /// <summary>
        /// 固定头首字节，高 4 位为报文类型、低 4 位为标志位
        /// </summary>
        public byte FixedHeader { get; }

        /// <summary>
        /// 报文体读取器，无报文体时为 null
        /// </summary>
        public IMqttPacketBodyReader Body { get; }

        /// <summary>
        /// 固定头与报文体合计的字节数
        /// </summary>
        public int TotalLength { get; }
    }
}
