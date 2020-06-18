/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Commom
*文件名： ParamsHelper
*版本号： v5.0.0.1
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
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SAEA.Common
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
                string val = string.Empty;

                if (nameValues != null && nameValues.Any() && nameValues.TryGetValue(parma.Name, out val))
                {
                    if (parma.ParameterType == typeof(System.Int32))
                    {
                        if (int.TryParse(val, out int v))
                        {
                            list.Add(v);
                        }
                        else throw new Exception($"参数{parma.Name}值{val}不正确！");
                    }
                    else if (parma.ParameterType == typeof(System.Int64))
                    {
                        if (long.TryParse(val, out long v))
                        {
                            list.Add(v);
                        }
                        else throw new Exception($"参数{parma.Name}值{val}不正确！");
                    }
                    else if (parma.ParameterType == typeof(System.Single))
                    {
                        if (float.TryParse(val, out float v))
                        {
                            list.Add(v);
                        }
                        else throw new Exception($"参数{parma.Name}值{val}不正确！");
                    }
                    else if (parma.ParameterType == typeof(System.Double))
                    {
                        if (double.TryParse(val, out double v))
                        {
                            list.Add(v);
                        }
                        else throw new Exception($"参数{parma.Name}值{val}不正确！");
                    }
                    else if (parma.ParameterType == typeof(System.DateTime))
                    {
                        if (DateTime.TryParse(val, out DateTime v))
                        {
                            list.Add(v);
                        }
                        else throw new Exception($"参数{parma.Name}值{val}不正确！");
                    }
                    else if (parma.ParameterType == typeof(System.Boolean))
                    {
                        if (string.IsNullOrEmpty(val)) val = "false";

                        if (int.TryParse(val, out int iv))
                        {
                            if (iv == 0) val = "false";
                            else val = "true";
                        }

                        if (bool.TryParse(val, out bool v))
                        {
                            list.Add(v);
                        }
                        else throw new Exception($"参数{parma.Name}值{val}不正确！");
                    }
                    else if (parma.ParameterType == typeof(System.Byte))
                    {
                        if (byte.TryParse(val, out byte v))
                        {
                            list.Add(v);
                        }
                        else throw new Exception($"参数{parma.Name}值{val}不正确！");
                    }
                    else if (parma.ParameterType == typeof(System.String))
                    {
                        list.Add(val);
                    }
                    else
                    {
                        throw new Exception($"参数{parma.Name}值{val}不正确！");
                    }
                }
                else
                {
                    if (parma.ParameterType == typeof(System.Int32))
                    {
                        list.Add(0);
                    }
                    else if (parma.ParameterType == typeof(System.Int64))
                    {
                        list.Add(0L);
                    }
                    else if (parma.ParameterType == typeof(System.Single))
                    {
                        list.Add(0F);
                    }
                    else if (parma.ParameterType == typeof(System.Double))
                    {
                        list.Add(0D);
                    }
                    else if (parma.ParameterType == typeof(System.DateTime))
                    {
                        list.Add(new DateTime());
                    }
                    else if (parma.ParameterType == typeof(System.Boolean))
                    {
                        list.Add(true);
                    }
                    else if (parma.ParameterType == typeof(System.Byte))
                    {
                        list.Add((byte)0);
                    }
                    else if (parma.ParameterType == typeof(System.String))
                    {
                        list.Add(string.Empty);
                    }
                    else
                    {
                        var modelType = parma.ParameterType;

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

                                    if (property.PropertyType == typeof(System.Int32))
                                    {
                                        if (int.TryParse(val, out int v))
                                        {
                                            property.SetValue(model, v);
                                        }
                                        else throw new Exception($"参数{property.Name}值{val}不正确！");
                                    }
                                    else if (property.PropertyType == typeof(System.Int64))
                                    {
                                        if (long.TryParse(val, out long v))
                                        {
                                            property.SetValue(model, v);
                                        }
                                        else throw new Exception($"参数{property.Name}值{val}不正确！");
                                    }
                                    else if (property.PropertyType == typeof(System.Single))
                                    {
                                        if (float.TryParse(val, out float v))
                                        {
                                            property.SetValue(model, v);
                                        }
                                        else throw new Exception($"参数{property.Name}值{val}不正确！");
                                    }
                                    else if (property.PropertyType == typeof(System.Double))
                                    {
                                        if (double.TryParse(val, out double v))
                                        {
                                            property.SetValue(model, v);
                                        }
                                        else throw new Exception($"参数{property.Name}值{val}不正确！");
                                    }
                                    else if (property.PropertyType == typeof(System.DateTime))
                                    {
                                        if (DateTime.TryParse(val, out DateTime v))
                                        {
                                            property.SetValue(model, v);
                                        }
                                        else throw new Exception($"参数{property.Name}值{val}不正确！");
                                    }
                                    else if (property.PropertyType == typeof(System.Boolean))
                                    {
                                        if (string.IsNullOrEmpty(val)) val = "false";

                                        if (int.TryParse(val, out int iv))
                                        {
                                            if (iv == 0) val = "false";
                                            else val = "true";
                                        }

                                        if (bool.TryParse(val, out bool v))
                                        {
                                            property.SetValue(model, v);
                                        }
                                        else throw new Exception($"参数{property.Name}值{val}不正确！");
                                    }
                                    else if (property.PropertyType == typeof(System.Byte))
                                    {
                                        if (byte.TryParse(val, out byte v))
                                        {
                                            property.SetValue(model, v);
                                        }
                                        else throw new Exception($"参数{property.Name}值{val}不正确！");
                                    }
                                    else if (property.PropertyType == typeof(System.String))
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
            if (list.Count == 0) return null;
            return list;
        }



    }
}
