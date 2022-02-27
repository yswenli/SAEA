/****************************************************************************
*Copyright (c) 2018-2022yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Commom
*文件名： ByteOrder
*版本号： v7.0.0.1
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/

using System;

namespace SAEA.Common
{
    public static class EndianHelper
    {
        /// <summary>
        /// 判断大小端
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        public static bool IsHostOrder(this EndianOrder order)
        {
            return !(BitConverter.IsLittleEndian ^ (order == EndianOrder.Little));
        }

        public static byte[] InternalToByteArray(this ulong value, EndianOrder order)
        {
            var bytes = BitConverter.GetBytes(value);
            if (!order.IsHostOrder())
                Array.Reverse(bytes);

            return bytes;
        }

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
        Little,
        Big
    }


    /// <summary>
    /// 编码排序
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Struct)]
    public class EndianAttribute : Attribute
    {
        public EndianAttribute(EndianOrder byteOrder)
        {
            this.ByteOrder = byteOrder;
        }

        public EndianOrder ByteOrder
        {
            get;
            private set;
        }
    }
}
