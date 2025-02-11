/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Commom
*文件名： ParamsHelper
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
        public static void NotNull(this string[] @params)
        {
            if (@params == null) throw new Exception("params must not allow null");

            foreach (var item in @params)
            {
                if (item == null) throw new Exception("params must not allow null");
            }
        }

        public static void NotNull(this List<string> list)
        {
            if (list == null) throw new Exception("list must not allow null");

            foreach (var item in list)
            {
                if (item == null) throw new Exception("list must not allow null");
            }
        }

        public static void NotNull(this IEnumerable<string> list)
        {
            if (list == null) throw new Exception("list must not allow null");

            foreach (var item in list)
            {
                if (item == null) throw new Exception("list must not allow null");
            }
        }

        public static void NotNull(this Dictionary<string, string> dic)
        {
            if (dic == null) throw new Exception("params must not allow null");

            foreach (var item in dic)
            {
                if (string.IsNullOrEmpty(item.Key) || item.Value == null) throw new Exception("params must not allow null");
            }
        }

        public static void NotNull(this Dictionary<double, string> dic)
        {
            if (dic == null) throw new Exception("params must not allow null");

            foreach (var item in dic)
            {
                if (item.Value == null) throw new Exception("value must not allow null");
            }
        }


        public static void KeyCheck(this string key)
        {
            if (string.IsNullOrEmpty(key)) throw new Exception("key must not allow null");
        }

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
