/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.WebAPI.Common
*文件名： SerializeHelper
*版本号： V1.0.0.0
*唯一标识：924c692f-764b-4373-bbab-5bc523ab03de
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/11 15:37:23
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/11 15:37:23
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.WebAPI.Common
{
    /// <summary>
    /// json 序例化
    /// </summary>
    public static class SerializeHelper
    {
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
    }
}
