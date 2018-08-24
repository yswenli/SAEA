/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Commom
*文件名： Class1
*版本号： V1.0.0.0
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
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using System;
using System.IO;

namespace SAEA.Common
{
    public static class ByteHelper
    {
        public static byte[] ConvertToBytes(long num)
        {
            return BitConverter.GetBytes(num);
        }

        public static byte[] ToBytes(this long num)
        {
            return ConvertToBytes(num);
        }

        public static byte[] ConvertToBytes(int num)
        {
            return BitConverter.GetBytes(num);
        }

        public static byte[] ToBytes(this int num)
        {
            return ConvertToBytes(num);
        }


        public static long ConvertToLong(byte[] data, int offset = 0)
        {
            return BitConverter.ToInt64(data, offset);
        }

        public static long ToLong(this byte[] data, int offset = 0)
        {
            return ConvertToLong(data, offset);
        }

        public static int ToInt(this byte[] data, int offset = 0)
        {
            return BitConverter.ToInt32(data, offset);
        }

        public static byte[] InternalToByteArray(this ulong value, ByteOrder order)
        {
            var bytes = BitConverter.GetBytes(value);
            if (!order.IsHostOrder())
                Array.Reverse(bytes);

            return bytes;
        }

        public static byte[] InternalToByteArray(this ushort value, ByteOrder order)
        {
            var bytes = BitConverter.GetBytes(value);
            if (!order.IsHostOrder())
                Array.Reverse(bytes);

            return bytes;
        }

        public static void WriteBytes(this Stream stream, byte[] bytes, int bufferLength)
        {
            using (var input = new MemoryStream(bytes))
                input.CopyTo(stream, bufferLength);
        }

        public static bool IsHostOrder(this ByteOrder order)
        {
            return !(BitConverter.IsLittleEndian ^ (order == ByteOrder.Little));
        }
        /// <summary>
        /// 查找数据是存在的位置
        /// </summary>
        /// <param name="data"></param>
        /// <param name="searchData"></param>
        /// <param name="start"></param>
        /// <returns></returns>
        public static int IndexOf(this byte[] data, byte[] searchData, int start = 0)
        {
            int result = -1;
            var total = data.Length;
            var count = searchData.Length;
            var remain = total - start;

            var last = remain - count;

            if (last >= 0)
            {
                var finded = true;
                for (int i = start; i < last + 1; i++)
                {
                    finded = true;
                    for (int j = 0; j < count; j++)
                    {
                        if (data[i + j] != searchData[j])
                        {
                            finded = false;
                            break;
                        }
                    }
                    if (finded)
                    {
                        result = i;
                        break;
                    }
                }
            }

            return result;
        }
    }
}
