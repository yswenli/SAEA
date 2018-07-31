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
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace SAEA.Common
{
    public static class SerializeHelper
    {
        /// <summary>
        ///     二进制序列化
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static byte[] ByteSerialize<T>(T obj)
        {
            using (var m = new MemoryStream())
            {
                m.Position = 0;
                var bin = new BinaryFormatter();
                bin.Serialize(m, obj);
                m.Position = 0;
                return m.ToArray();
            }
        }

        /// <summary>
        ///     二进制反序列化
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public static T ByteDeserialize<T>(byte[] buffer)
        {
            using (var m = new MemoryStream())
            {
                m.Position = 0;
                m.Write(buffer, 0, buffer.Length);
                var bin = new BinaryFormatter();
                m.Position = 0;
                return (T)bin.Deserialize(m);
            }
        }
        #region byte序列化扩展
        public static byte[] ToBytes<T>(this T t) where T : class, new()
        {
            return ByteSerialize<T>(t);
        }

        public static byte[] ToBytes<T>(this List<T> t) where T : class, new()
        {
            return ByteSerialize(t);
        }

        public static byte[] ToBytes(this List<byte[]> t)
        {
            return ByteSerialize(t);
        }

        public static T ToInstance<T>(this byte[] buffer) where T : class, new()
        {
            return ByteDeserialize<T>(buffer);
        }

        public static List<T> ToList<T>(this byte[] buffer) where T : class, new()
        {
            return ByteDeserialize<List<T>>(buffer);
        }
        public static List<byte[]> ToList(this byte[] buffer)
        {
            return ByteDeserialize<List<byte[]>>(buffer);
        }
        #endregion

        #region json
        /// <summary>
        ///     newton.json序列化,日志参数专用
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string Serialize(object obj)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
            settings.DateFormatString = "yyyy-MM-dd HH:mm:ss.fff";
            return JsonConvert.SerializeObject(obj, settings);
        }

        /// <summary>
        ///     newton.json反序列化,日志参数专用
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="json"></param>
        /// <returns></returns>
        public static T Deserialize<T>(string json)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
            settings.DateFormatString = "yyyy-MM-dd HH:mm:ss.fff";
            return JsonConvert.DeserializeObject<T>(json, settings);
        }
        #endregion

    }
}
