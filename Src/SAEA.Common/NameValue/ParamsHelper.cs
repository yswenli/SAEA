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
*命名空间：SAEA.Common.NameValue
*文件名： ParamsHelper
*版本号： v26.4.23.1
*唯一标识：9edd1f68-5aef-418c-9979-e6f5f1ea25d8
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/16 17:50:24
*描述：ParamsHelper帮助类
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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SAEA.Common.NameValue
{
    /// <summary>
    /// 反射填充类
    /// </summary>
    public static class ParamsHelper
    {
        /// <summary>
        /// 校验字符串数组及其元素均不为 null，否则抛出异常
        /// </summary>
        /// <param name="params">待校验的字符串数组</param>
        public static void NotNull(this string[] @params)
        {
            if (@params == null) throw new Exception("params must not allow null");

            foreach (var item in @params)
            {
                if (item == null) throw new Exception("params must not allow null");
            }
        }

        /// <summary>
        /// 校验列表及其元素均不为 null，否则抛出异常
        /// </summary>
        /// <param name="list">待校验的列表</param>
        public static void NotNull(this List<string> list)
        {
            if (list == null) throw new Exception("list must not allow null");

            foreach (var item in list)
            {
                if (item == null) throw new Exception("list must not allow null");
            }
        }

        /// <summary>
        /// 校验序列及其元素均不为 null，否则抛出异常
        /// </summary>
        /// <param name="list">待校验的字符串序列</param>
        public static void NotNull(this IEnumerable<string> list)
        {
            if (list == null) throw new Exception("list must not allow null");

            foreach (var item in list)
            {
                if (item == null) throw new Exception("list must not allow null");
            }
        }

        /// <summary>
        /// 校验字典不为 null、键非空且值不为 null，否则抛出异常
        /// </summary>
        /// <param name="dic">待校验的字典</param>
        public static void NotNull(this Dictionary<string, string> dic)
        {
            if (dic == null) throw new Exception("params must not allow null");

            foreach (var item in dic)
            {
                if (string.IsNullOrEmpty(item.Key) || item.Value == null) throw new Exception("params must not allow null");
            }
        }

        /// <summary>
        /// 校验字典不为 null 且值均不为 null，否则抛出异常
        /// </summary>
        /// <param name="dic">待校验的字典</param>
        public static void NotNull(this Dictionary<double, string> dic)
        {
            if (dic == null) throw new Exception("params must not allow null");

            foreach (var item in dic)
            {
                if (item.Value == null) throw new Exception("value must not allow null");
            }
        }


        /// <summary>
        /// 校验单个键不为 null 或空字符串，否则抛出异常
        /// </summary>
        /// <param name="key">待校验的键</param>
        public static void KeyCheck(this string key)
        {
            if (string.IsNullOrEmpty(key)) throw new Exception("key must not allow null");
        }

        /// <summary>
        /// 校验键数组不为 null 且每个键都非空，否则抛出异常
        /// </summary>
        /// <param name="keys">待校验的键数组</param>
        public static void KeyCheck(this string[] keys)
        {
            if (keys == null) throw new Exception("key must not allow null");
            foreach (var key in keys)
            {
                if (string.IsNullOrEmpty(key)) throw new Exception("key must not allow null");
            }
        }

        /// <summary>
        /// 参数填充
        /// </summary>
        /// <param name="params"></param>
        /// <param name="nameValues"></param>
        /// <returns></returns>
        public static List<object> FillPamars(ParameterInfo[] @params, NameValueCollection nameValues)
        {
            List<object> list = new List<object>();

            foreach (var parma in @params)
            {
                object val = string.Empty;

                if (nameValues != null && nameValues.Any())
                {
                    nameValues.TryGetValue(parma.Name, out val);

                    if (parma.ParameterType == typeof(Int32) || parma.ParameterType == typeof(Nullable<Int32>))
                    {
                        if (int.TryParse(val.ToString(), out int v))
                        {
                            list.Add(v);
                        }
                        else
                        {
                            list.Add(0);
                        }
                    }
                    else if (parma.ParameterType == typeof(Int16) || parma.ParameterType == typeof(Nullable<Int16>))
                    {
                        if (int.TryParse(val.ToString(), out int v))
                        {
                            list.Add(v);
                        }
                        else
                        {
                            list.Add(0);
                        }
                    }
                    else if (parma.ParameterType == typeof(Int64) || parma.ParameterType == typeof(Nullable<Int64>))
                    {
                        if (long.TryParse(val.ToString(), out long v))
                        {
                            list.Add(v);
                        }
                        else
                        {
                            list.Add(0);
                        }
                    }
                    else if (parma.ParameterType == typeof(Single) || parma.ParameterType == typeof(Nullable<Single>))
                    {
                        if (float.TryParse(val.ToString(), out float v))
                        {
                            list.Add(v);
                        }
                        else
                        {
                            list.Add(0);
                        }
                    }
                    else if (parma.ParameterType == typeof(Double) || parma.ParameterType == typeof(Nullable<double>))
                    {
                        if (double.TryParse(val.ToString(), out double v))
                        {
                            list.Add(v);
                        }
                        else
                        {
                            list.Add(0);
                        }
                    }
                    else if (parma.ParameterType == typeof(DateTime) || parma.ParameterType == typeof(Nullable<DateTime>))
                    {
                        if (DateTime.TryParse(val.ToString(), out DateTime v))
                        {
                            list.Add(v);
                        }
                        else
                        {
                            list.Add(new DateTime());
                        }
                    }
                    else if (parma.ParameterType == typeof(Boolean) || parma.ParameterType == typeof(Nullable<bool>))
                    {
                        if (val == null || string.IsNullOrEmpty(val.ToString())) val = "false";

                        if (int.TryParse(val.ToString(), out int iv))
                        {
                            if (iv == 0) val = "false";
                            else val = "true";
                        }

                        if (bool.TryParse(val.ToString(), out bool v))
                        {
                            list.Add(v);
                        }
                        else
                        {
                            list.Add(false);
                        }
                    }
                    else if (parma.ParameterType == typeof(Byte) || parma.ParameterType == typeof(Nullable<byte>))
                    {
                        if (byte.TryParse(val.ToString(), out byte v))
                        {
                            list.Add(v);
                        }
                        else
                        {
                            list.Add(0);
                        }
                    }
                    else if (parma.ParameterType == typeof(System.String))
                    {
                        list.Add(val);
                    }
                    else
                    {
                        var modelType = parma.ParameterType;

                        if (val != null && modelType.Name == val.GetType().Name)
                        {
                            list.Add(val);
                        }
                        else
                        {
                            var model = Activator.CreateInstance(modelType);

                            var properties = modelType.GetProperties();

                            if (properties != null)
                            {
                                foreach (var property in properties)
                                {
                                    var item = nameValues.Get(property.Name);

                                    if (item != null)
                                    {
                                        val = item.Value;

                                        if (property.PropertyType == typeof(Int32) || property.PropertyType == typeof(Nullable<int>))
                                        {
                                            if (int.TryParse(val.ToString(), out int v))
                                            {
                                                property.SetValue(model, v);
                                            }
                                            else
                                            {
                                                property.SetValue(model, 0);
                                            }
                                        }
                                        else if (property.PropertyType == typeof(Int16) || property.PropertyType == typeof(Nullable<Int16>))
                                        {
                                            if (int.TryParse(val.ToString(), out int v))
                                            {
                                                property.SetValue(model, v);
                                            }
                                            else
                                            {
                                                property.SetValue(model, 0);
                                            }
                                        }
                                        else if (property.PropertyType == typeof(Int64) || property.PropertyType == typeof(Nullable<Int64>))
                                        {
                                            if (long.TryParse(val.ToString(), out long v))
                                            {
                                                property.SetValue(model, v);
                                            }
                                            else
                                            {
                                                property.SetValue(model, 0);
                                            }
                                        }
                                        else if (property.PropertyType == typeof(Single) || property.PropertyType == typeof(Nullable<Single>))
                                        {
                                            if (float.TryParse(val.ToString(), out float v))
                                            {
                                                property.SetValue(model, v);
                                            }
                                            else
                                            {
                                                property.SetValue(model, 0);
                                            }
                                        }
                                        else if (property.PropertyType == typeof(Double) || property.PropertyType == typeof(Nullable<Double>))
                                        {
                                            if (double.TryParse(val.ToString(), out double v))
                                            {
                                                property.SetValue(model, v);
                                            }
                                            else
                                            {
                                                property.SetValue(model, 0);
                                            }
                                        }
                                        else if (property.PropertyType == typeof(DateTime) || property.PropertyType == typeof(Nullable<DateTime>))
                                        {
                                            if (DateTime.TryParse(val.ToString(), out DateTime v))
                                            {
                                                property.SetValue(model, v);
                                            }
                                            else
                                            {
                                                property.SetValue(model, new DateTime());
                                            }
                                        }
                                        else if (property.PropertyType == typeof(Boolean) || property.PropertyType == typeof(Nullable<Boolean>))
                                        {
                                            if (string.IsNullOrEmpty(val.ToString())) val = "false";

                                            if (int.TryParse(val.ToString(), out int iv))
                                            {
                                                if (iv == 0) val = "false";
                                                else val = "true";
                                            }

                                            if (bool.TryParse(val.ToString(), out bool v))
                                            {
                                                property.SetValue(model, v);
                                            }
                                            else
                                            {
                                                property.SetValue(model, false);
                                            }
                                        }
                                        else if (property.PropertyType == typeof(Byte) || property.PropertyType == typeof(Nullable<Byte>))
                                        {
                                            if (byte.TryParse(val.ToString(), out byte v))
                                            {
                                                property.SetValue(model, v);
                                            }
                                            else
                                            {
                                                property.SetValue(model, 0);
                                            }
                                        }
                                        else if (property.PropertyType == typeof(String))
                                        {
                                            property.SetValue(model, val);
                                        }
                                    }
                                }

                                list.Add(model);
                            }
                            else
                            {
                                list.Add(null);
                            }
                        }
                    }
                }
                else
                {
                    if (parma.ParameterType == typeof(Int32) || parma.ParameterType == typeof(Nullable<Int32>))
                    {
                        list.Add(0);
                    }
                    else if (parma.ParameterType == typeof(Int16) || parma.ParameterType == typeof(Nullable<Int16>))
                    {
                        list.Add(0L);
                    }
                    else if (parma.ParameterType == typeof(Int64) || parma.ParameterType == typeof(Nullable<Int64>))
                    {
                        list.Add(0L);
                    }
                    else if (parma.ParameterType == typeof(Single) || parma.ParameterType == typeof(Nullable<Single>))
                    {
                        list.Add(0F);
                    }
                    else if (parma.ParameterType == typeof(Double) || parma.ParameterType == typeof(Nullable<double>))
                    {
                        list.Add(0D);
                    }
                    else if (parma.ParameterType == typeof(DateTime) || parma.ParameterType == typeof(Nullable<DateTime>))
                    {
                        list.Add(new DateTime());
                    }
                    else if (parma.ParameterType == typeof(Boolean) || parma.ParameterType == typeof(Nullable<bool>))
                    {
                        list.Add(true);
                    }
                    else if (parma.ParameterType == typeof(Byte) || parma.ParameterType == typeof(Nullable<byte>))
                    {
                        list.Add((byte)0);
                    }
                    else if (parma.ParameterType == typeof(String))
                    {
                        list.Add(string.Empty);
                    }
                    else
                    {
                        list.Add(null);
                    }
                }
            }
            if (list.Count == 0) return null;
            return list;
        }



    }
}