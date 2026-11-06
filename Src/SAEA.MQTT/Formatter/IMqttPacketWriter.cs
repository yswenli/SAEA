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
*文件名： IMqttPacketWriter
*版本号： v26.4.23.1
*唯一标识：7e255919-cdc2-4314-bef6-e66eb4158e63
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：IMqttPacketWriter接口
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
    /// MQTT 报文写入器，按协议编码规则顺序写入字段并管理内部缓冲区
    /// </summary>
    public interface IMqttPacketWriter
    {
        /// <summary>
        /// 已写入的有效字节数
        /// </summary>
        int Length { get; }

        /// <summary>
        /// 写入带两字节长度前缀的 UTF-8 字符串，空值写为长度 0
        /// </summary>
        /// <param name="value">待写入的字符串，可为 null</param>
        void WriteWithLengthPrefix(string value);

        /// <summary>
        /// 写入单字节
        /// </summary>
        /// <param name="value">待写入的字节</param>
        void Write(byte value);

        /// <summary>
        /// 写入带两字节长度前缀的二进制数据，空数组或 null 写为长度 0
        /// </summary>
        /// <param name="value">待写入的数据，可为 null</param>
        void WriteWithLengthPrefix(byte[] value);

        /// <summary>
        /// 以大端序写入两字节整数
        /// </summary>
        /// <param name="value">待写入的数值</param>
        void Write(ushort value);

        /// <summary>
        /// 追加另一个写入器已写入的内容
        /// </summary>
        /// <param name="value">作为数据来源的写入器，无内容时忽略</param>
        void Write(IMqttPacketWriter value);

        /// <summary>
        /// 写入变长字节整数
        /// </summary>
        /// <param name="value">待写入的数值</param>
        void WriteVariableLengthInteger(uint value);

        /// <summary>
        /// 写入字节数组的指定片段
        /// </summary>
        /// <param name="value">数据源</param>
        /// <param name="offset">数据源起始偏移</param>
        /// <param name="length">要写入的字节数</param>
        void Write(byte[] value, int offset, int length);

        /// <summary>
        /// 重置已写入长度为指定值，写入位置需另行调用 Seek 设置
        /// </summary>
        /// <param name="length">新的已写入长度</param>
        void Reset(int length);

        /// <summary>
        /// 移动写入位置，必要时扩容
        /// </summary>
        /// <param name="offset">目标位置</param>
        void Seek(int offset);

        /// <summary>
        /// 释放并收缩编码缓冲区
        /// </summary>
        void FreeBuffer();

        /// <summary>
        /// 获取内部缓冲区，有效数据为前 Length 个字节
        /// </summary>
        /// <returns>内部缓冲区数组</returns>
        byte[] GetBuffer();
    }
}
