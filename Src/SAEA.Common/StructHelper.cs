/****************************************************************************
*项目名称：SAEA.DNS
*CLR 版本：3.0
*机器名称：WENLI-PC
*命名空间：SAEA.DNS.Common.Marshalling
*类 名 称：StructHelper
*版 本 号：v5.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/11/28 22:43:28
*描述：
*=====================================================================
*修改时间：2019/11/28 22:43:28
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
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
