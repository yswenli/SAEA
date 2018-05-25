/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RPC.Serialize
*文件名： JsonSerializeUtil
*版本号： V1.0.0.0
*唯一标识：0984c81f-448f-4575-bb2a-038a4d0a8d2e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/23 16:56:26
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/23 16:56:26
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RPC.Serialize
{
    public static class JsonSerializeUtil
    {
        static JsonSerializerSettings _settings;

        static JsonSerializeUtil()
        {
            _settings = new JsonSerializerSettings();
            _settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
            _settings.DateFormatString = "yyyy-MM-dd HH:mm:ss.fff";
        }

        /// <summary>
        /// newton.json序列化
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj, _settings);
        }

        /// <summary>
        /// newton.json反序列化
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="json"></param>
        /// <returns></returns>
        public static T Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, _settings);
        }

        public static object DeserializeObject(string json, Type type)
        {            
            return JsonConvert.DeserializeObject(json, type, _settings);
        }

    }
}
