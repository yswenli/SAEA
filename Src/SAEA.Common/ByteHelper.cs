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
*唯一标识：c4ddf607-5eb0-43e2-988a-103f2164ad14
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/08/24 16:31:11
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/08/24 16:31:11
*修改人： yswenli
*版本号： v26.4.23.1
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

        public static byte GetBitValueAt(this byte b, byte offset, byte length)
        {
            return (byte)((b >> offset) & ~(0xff << length));
        }

        public static byte GetBitValueAt(this byte b, byte offset)
        {
            return b.GetBitValueAt(offset, 1);
        }

        public static byte SetBitValueAt(this byte b, byte offset, byte length, byte value)
        {
            int mask = ~(0xff << length);
            value = (byte)(value & mask);

            return (byte)((value << offset) | (b & ~(mask << offset)));
        }

        public static byte SetBitValueAt(this byte b, byte offset, byte value)
        {
            return b.SetBitValueAt(offset, 1, value);
        }

        #endregion

        public static void Clear(this byte[] bytes)
        {
            if (bytes == null || bytes.Length < 1) return;
            Array.Clear(bytes, 0, bytes.Length);
        }
    }
}