/****************************************************************************
*Copyright (c) 2018-2022yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Commom.Serialization
*文件名： Class1
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
using SAEA.Common.Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;

namespace SAEA.Common.Serialization
{
    /// <summary>
    /// 常规序列化工具类
    /// </summary>
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
        /// newton.json序列化
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="expended"></param>
        /// <returns></returns>
        public static string Serialize(object obj, bool expended = false)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
            settings.DateFormatString = "yyyy-MM-dd HH:mm:ss.fff";
            if (expended)
            {
                settings.Formatting = Formatting.Indented;
            }
            return JsonConvert.SerializeObject(obj, settings);
        }

        /// <summary>
        /// newton.json反序列化
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

        /// <summary>
        /// newton.json反序列化
        /// </summary>
        /// <param name="json"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static object Deserialize(string json, Type type)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
            settings.DateFormatString = "yyyy-MM-dd HH:mm:ss.fff";
            return JsonConvert.DeserializeObject(json, type, settings);
        }

        /// <summary>
        /// newton.json反序列化
        /// </summary>
        /// <param name="json"></param>
        /// <param name="types"></param>
        /// <returns></returns>
        public static List<object> Deserialize(string json, Type[] types)
        {
            List<object> list = new List<object>();

            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
            settings.DateFormatString = "yyyy-MM-dd HH:mm:ss.fff";
            JsonSerializer jsonSerializer = JsonSerializer.CreateDefault(settings);
            if (!jsonSerializer.IsCheckAdditionalContentSet())
                jsonSerializer.CheckAdditionalContent = true;

            using (var reader = new JsonTextReader(new StringReader(json)))
            {
                foreach (var type in types)
                {
                    list.Add(jsonSerializer.Deserialize(reader, type));
                }
            }
            return list;
        }

        /// <summary>
        /// 快捷将json转换成对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="json"></param>
        /// <returns></returns>
        public static T JsonToObj<T>(this string json)
        {
            if (!string.IsNullOrEmpty(json))
            {
                return Deserialize<T>(json);
            }
            return default(T);
        }
        /// <summary>
        /// 快捷将json转换成对象列表
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="jsons"></param>
        /// <returns></returns>
        public static List<T> JsonToObj<T>(this List<string> jsons)
        {
            if (jsons == null || !jsons.Any()) return null;

            List<T> result = new List<T>();

            foreach (var item in jsons)
            {
                result.Add(item.JsonToObj<T>());
            }

            return result;
        }
        #endregion

        #region ProtoBuffer

        /// <summary>
        /// 序列化
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="t"></param>
        /// <returns></returns>
        public static byte[] PBSerialize<T>(T t)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ProtoBuf.Serializer.Serialize<T>(ms, t);
                byte[] result = new byte[ms.Length];
                ms.Position = 0;
                ms.Read(result, 0, result.Length);
                return result;
            }
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        public static T PBDeserialize<T>(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(data, 0, data.Length);
                ms.Position = 0;
                return (T)ProtoBuf.Serializer.Deserialize<T>(ms);
            }
        }

        #endregion

    }

}
