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
*命名空间：SAEA.Common
*文件名： EndianHelper
*版本号： v26.4.23.1
*唯一标识：108e153a-b56d-4ff3-80e9-e4179d9224c7
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/30 23:44:42
*描述：EndianHelper接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:45
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;

namespace SAEA.Common
{
    /// <summary>
    /// 本机字节序与指定字节序的判定及整数转字节数组
    /// </summary>
    public static class EndianHelper
    {
        /// <summary>
        /// 判断指定字节序是否与本机字节序一致
        /// </summary>
        /// <param name="order">待判断的字节序</param>
        /// <returns>与本机字节序一致返回 true</returns>
        public static bool IsHostOrder(this EndianOrder order)
        {
            return !(BitConverter.IsLittleEndian ^ (order == EndianOrder.Little));
        }

        /// <summary>
        /// 将 64 位无符号整数按指定字节序转为 8 字节数组
        /// </summary>
        /// <param name="value">待转换的整数</param>
        /// <param name="order">目标字节序</param>
        /// <returns>转换后的字节数组</returns>
        public static byte[] InternalToByteArray(this ulong value, EndianOrder order)
        {
            var bytes = BitConverter.GetBytes(value);
            if (!order.IsHostOrder())
                Array.Reverse(bytes);

            return bytes;
        }

        /// <summary>
        /// 将 16 位无符号整数按指定字节序转为 2 字节数组
        /// </summary>
        /// <param name="value">待转换的整数</param>
        /// <param name="order">目标字节序</param>
        /// <returns>转换后的字节数组</returns>
        public static byte[] InternalToByteArray(this ushort value, EndianOrder order)
        {
            var bytes = BitConverter.GetBytes(value);
            if (!order.IsHostOrder())
                Array.Reverse(bytes);

            return bytes;
        }
    }

    /// <summary>
    /// 大小端编码
    /// </summary>
    public enum EndianOrder
    {
        /// <summary>
        /// 小端序，低位字节在前
        /// </summary>
        Little,
        /// <summary>
        /// 大端序，高位字节在前
        /// </summary>
        Big
    }


    /// <summary>
    /// 编码排序
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Struct)]
    public class EndianAttribute : Attribute
    {
        /// <summary>
        /// 标记字段或结构体的字节序
        /// </summary>
        /// <param name="byteOrder">该字段或结构体使用的字节序</param>
        public EndianAttribute(EndianOrder byteOrder)
        {
            this.ByteOrder = byteOrder;
        }

        /// <summary>
        /// 标记的字节序
        /// </summary>
        public EndianOrder ByteOrder
        {
            get;
            private set;
        }
    }
}