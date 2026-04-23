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
*文件名： StructHelper
*版本号： v26.4.23.1
*唯一标识：334fd0a9-7f9a-47da-9906-2f5e74340451
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/30 23:44:42
*描述：
*
*=====================================================================
*修改标记
*修改时间：2019/11/30 23:44:42
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Linq;

namespace SAEA.Common
{
    /// <summary>
    /// 结构体辅助类,
    /// 序列化与反序列化
    /// </summary>
    public static class StructHelper
    {
        /// <summary>
        /// 转换大小端编码
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        private static byte[] ConvertEndian<T>(byte[] data)
        {
            Type type = typeof(T);
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            EndianAttribute endian = null;

            if (type.GetTypeInfo().IsDefined(typeof(EndianAttribute), false))
            {
                endian = (EndianAttribute)type.GetTypeInfo().GetCustomAttributes(typeof(EndianAttribute), false).First();
            }

            foreach (FieldInfo field in fields)
            {
                if (endian == null && !field.IsDefined(typeof(EndianAttribute), false))
                {
                    continue;
                }

                int offset = Marshal.OffsetOf<T>(field.Name).ToInt32();

                int length = Marshal.SizeOf(field.FieldType);

                endian = endian ?? (EndianAttribute)field.GetCustomAttributes(typeof(EndianAttribute), false).First();

                if (endian.ByteOrder == EndianOrder.Big && BitConverter.IsLittleEndian ||
                        endian.ByteOrder == EndianOrder.Little && !BitConverter.IsLittleEndian)
                {
                    Array.Reverse(data, offset, length);
                }
            }

            return data;
        }

        /// <summary>
        /// 转换成struct
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        public static T GetStruct<T>(byte[] data) where T : struct
        {
            return GetStruct<T>(data, 0, data.Length);
        }

        /// <summary>
        /// 转换成struct
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static T GetStruct<T>(byte[] data, int offset, int length) where T : struct
        {
            byte[] buffer = new byte[length];
            Array.Copy(data, offset, buffer, 0, buffer.Length);

            GCHandle handle = GCHandle.Alloc(ConvertEndian<T>(buffer), GCHandleType.Pinned);

            try
            {
                return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        /// 转换成byte[]
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static byte[] GetBytes<T>(T obj) where T : struct
        {
            byte[] data = new byte[Marshal.SizeOf(obj)];
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);

            try
            {
                Marshal.StructureToPtr(obj, handle.AddrOfPinnedObject(), false);
                return ConvertEndian<T>(data);
            }
            finally
            {
                handle.Free();
            }
        }
    }
}
