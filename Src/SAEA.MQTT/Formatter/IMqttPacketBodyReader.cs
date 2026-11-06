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
*文件名： IMqttPacketBodyReader
*版本号： v26.4.23.1
*唯一标识：5813a5af-18aa-4907-8c3f-bde56872833f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttPacketBodyReader类
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
    /// MQTT 报文体读取器，按协议编码规则顺序读取各类字段并推进读取位置
    /// </summary>
    public interface IMqttPacketBodyReader
    {
        /// <summary>
        /// 报文体中尚未读取的字节数
        /// </summary>
        int Length { get; }

        /// <summary>
        /// 当前读取位置在底层缓冲区中的索引
        /// </summary>
        int Offset { get; }

        /// <summary>
        /// 读取位置是否已到达报文体末尾
        /// </summary>
        bool EndOfStream { get; }

        /// <summary>
        /// 读取单字节
        /// </summary>
        /// <returns>读取到的字节</returns>
        byte ReadByte();

        /// <summary>
        /// 读取从当前位置到末尾的全部数据，返回独立副本
        /// </summary>
        /// <returns>剩余数据副本，修改它不影响底层缓冲区</returns>
        byte[] ReadRemainingData();

        /// <summary>
        /// 读取两字节整数（大端序）
        /// </summary>
        /// <returns>解码后的无符号两字节整数</returns>
        ushort ReadTwoByteInteger();

        /// <summary>
        /// 读取带两字节长度前缀的 UTF-8 字符串
        /// </summary>
        /// <returns>解码后的字符串</returns>
        string ReadStringWithLengthPrefix();

        /// <summary>
        /// 读取带两字节长度前缀的二进制数据
        /// </summary>
        /// <returns>读取到的字节数组</returns>
        byte[] ReadWithLengthPrefix();

        /// <summary>
        /// 读取四字节整数（大端序）
        /// </summary>
        /// <returns>解码后的无符号四字节整数</returns>
        uint ReadFourByteInteger();

        /// <summary>
        /// 读取变长字节整数，每字节低 7 位有效、最高位表示是否继续读取
        /// </summary>
        /// <returns>解码后的整数值</returns>
        uint ReadVariableLengthInteger();

        /// <summary>
        /// 读取布尔值，仅 0 与 1 合法
        /// </summary>
        /// <returns>true 表示 1，false 表示 0</returns>
        bool ReadBoolean();

        /// <summary>
        /// 将读取位置移动到相对报文体起始的指定偏移
        /// </summary>
        /// <param name="position">相对报文体起始的偏移</param>
        void Seek(int position);
    }
}
