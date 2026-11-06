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
*文件名： ByteHelper
*版本号： v26.4.23.1
*唯一标识：b386762b-160c-4996-abd0-09d7d439796d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/08/24 16:31:11
*描述：ByteHelper帮助类
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
using System.IO;

namespace SAEA.Common
{
    /// <summary>
    /// 字节数组与数值互转、位读写、内容清空等操作
    /// </summary>
    public static class ByteHelper
    {
        /// <summary>
        /// 将 64 位整数转为 8 字节数组，字节序为本机字节序
        /// </summary>
        /// <param name="num">待转换的 64 位整数</param>
        /// <returns>长度为 8 的字节数组</returns>
        public static byte[] ConvertToBytes(long num)
        {
            return BitConverter.GetBytes(num);
        }

        /// <summary>
        /// 将 64 位整数转为 8 字节数组，字节序为本机字节序
        /// </summary>
        /// <param name="num">待转换的 64 位整数</param>
        /// <returns>长度为 8 的字节数组</returns>
        public static byte[] ToBytes(this long num)
        {
            return ConvertToBytes(num);
        }

        /// <summary>
        /// 将 32 位整数转为 4 字节数组，字节序为本机字节序
        /// </summary>
        /// <param name="num">待转换的 32 位整数</param>
        /// <returns>长度为 4 的字节数组</returns>
        public static byte[] ConvertToBytes(int num)
        {
            return BitConverter.GetBytes(num);
        }

        /// <summary>
        /// 将 32 位整数转为 4 字节数组，字节序为本机字节序
        /// </summary>
        /// <param name="num">待转换的 32 位整数</param>
        /// <returns>长度为 4 的字节数组</returns>
        public static byte[] ToBytes(this int num)
        {
            return ConvertToBytes(num);
        }


        /// <summary>
        /// 从字节数组指定位置读取 8 字节并转为 64 位整数，字节序为本机字节序
        /// </summary>
        /// <param name="data">源字节数组</param>
        /// <param name="offset">起始下标，默认 0</param>
        /// <returns>读取到的 64 位整数</returns>
        public static long ConvertToLong(byte[] data, int offset = 0)
        {
            return BitConverter.ToInt64(data, offset);
        }

        /// <summary>
        /// 从字节数组指定位置读取 8 字节并转为 64 位整数，字节序为本机字节序
        /// </summary>
        /// <param name="data">源字节数组</param>
        /// <param name="offset">起始下标，默认 0</param>
        /// <returns>读取到的 64 位整数</returns>
        public static long ToLong(this byte[] data, int offset = 0)
        {
            return ConvertToLong(data, offset);
        }

        /// <summary>
        /// 从字节数组指定位置读取 4 字节并转为 32 位整数，字节序为本机字节序
        /// </summary>
        /// <param name="data">源字节数组</param>
        /// <param name="offset">起始下标，默认 0</param>
        /// <returns>读取到的 32 位整数</returns>
        public static int ToInt(this byte[] data, int offset = 0)
        {
            return BitConverter.ToInt32(data, offset);
        }
        

        /// <summary>
        /// 将字节数组按每次 bufferLength 字节写入目标流
        /// </summary>
        /// <param name="stream">目标流</param>
        /// <param name="bytes">待写入的字节数组</param>
        /// <param name="bufferLength">单次复制的字节数</param>
        public static void WriteBytes(this Stream stream, byte[] bytes, int bufferLength)
        {
            using (var input = new MemoryStream(bytes))
                input.CopyTo(stream, bufferLength);
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

        #region bit op

        /// <summary>
        /// 取字节中指定偏移与位数的二进制位值
        /// </summary>
        /// <param name="b">源字节</param>
        /// <param name="offset">右移的位数，即起始位下标</param>
        /// <param name="length">要截取的位数</param>
        /// <returns>截取出的位值，范围随 length 变化</returns>
        public static byte GetBitValueAt(this byte b, byte offset, byte length)
        {
            return (byte)((b >> offset) & ~(0xff << length));
        }

        /// <summary>
        /// 取指定偏移处的单个二进制位值
        /// </summary>
        /// <param name="b">源字节</param>
        /// <param name="offset">右移的位数，即起始位下标</param>
        /// <returns>该位的值，0 或 1</returns>
        public static byte GetBitValueAt(this byte b, byte offset)
        {
            return b.GetBitValueAt(offset, 1);
        }

        /// <summary>
        /// 将指定偏移和位数的二进制位替换为给定值，保留其余位
        /// </summary>
        /// <param name="b">源字节</param>
        /// <param name="offset">左移的位数，即起始位下标</param>
        /// <param name="length">要覆盖的位数</param>
        /// <param name="value">新位值，超出 length 位的高位会被截断</param>
        /// <returns>替换后的字节</returns>
        public static byte SetBitValueAt(this byte b, byte offset, byte length, byte value)
        {
            int mask = ~(0xff << length);
            value = (byte)(value & mask);

            return (byte)((value << offset) | (b & ~(mask << offset)));
        }

        /// <summary>
        /// 设置指定偏移处的单个二进制位值，保留其余位
        /// </summary>
        /// <param name="b">源字节</param>
        /// <param name="offset">左移的位数，即起始位下标</param>
        /// <param name="value">新位值，取最低位</param>
        /// <returns>替换后的字节</returns>
        public static byte SetBitValueAt(this byte b, byte offset, byte value)
        {
            return b.SetBitValueAt(offset, 1, value);
        }

        #endregion

        /// <summary>
        /// 将字节数组全部元素置零，数组为 null 或空时直接返回
        /// </summary>
        /// <param name="bytes">待清空的字节数组</param>
        public static void Clear(this byte[] bytes)
        {
            if (bytes == null || bytes.Length < 1) return;
            Array.Clear(bytes, 0, bytes.Length);
        }
    }
}