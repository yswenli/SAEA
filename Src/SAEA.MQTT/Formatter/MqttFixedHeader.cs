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
*唯一标识：f88fcd04-e73d-4d4d-bdcb-d48e4a6a3e05
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MqttFixedHeader接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.MQTT.Formatter
{
    /// <summary>
    /// MQTT 固定头信息，记录首字节、剩余长度与报文总长度
    /// </summary>
    public struct MqttFixedHeader
    {
        /// <summary>
        /// 由已解析的固定头字段构造实例
        /// </summary>
        /// <param name="flags">固定头首字节，高 4 位为报文类型、低 4 位为报文标志</param>
        /// <param name="remainingLength">剩余长度，不含固定头自身占用的字节</param>
        /// <param name="totalLength">报文总字节数，含固定头的类型字节与剩余长度字段</param>
        public MqttFixedHeader(byte flags, int remainingLength, int totalLength)
        {
            Flags = flags;
            RemainingLength = remainingLength;
            TotalLength = totalLength;
        }

        /// <summary>
        /// 固定头首字节，高 4 位为报文类型、低 4 位为报文标志
        /// </summary>
        public byte Flags { get; }

        /// <summary>
        /// 剩余长度，即首字节之后、固定头剩余长度字段之外的报文体字节数
        /// </summary>
        public int RemainingLength { get; }

        /// <summary>
        /// 报文总字节数，含固定头的类型字节与剩余长度字段
        /// </summary>
        public int TotalLength { get; }
    }
}
