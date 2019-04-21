/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Commom
*文件名： ModelCloneHelper
*版本号： v4.5.1.2
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
*版本号： v4.5.1.2
*描述：
*
*****************************************************************************/
using System;
using System.Collections;
using System.ComponentModel;
using System.Reflection;

namespace SAEA.Common
{
    /// <summary>
    /// 实体复制工具类
    /// </summary>
    public static class ModelCloneHelper
    {
        /// <summary>
        /// 将一个对象转换为指定类型
        /// </summary>
        /// <param name="obj">待转换的对象</param>
        /// <param name="type">目标类型</param>
        /// <returns>转换后的对象</returns>
        public static object ConvertTo(this object obj, Type type)
        {
            if (type == null) return obj;

            if (obj == null) return type.IsValueType ? Activator.CreateInstance(type) : null;

            Type underlyingType = Nullable.GetUnderlyingType(type);

            if (type.IsAssignableFrom(obj.GetType()))
            {
                return obj;
            }
            else if ((underlyingType ?? type).IsEnum)
            {
                if (underlyingType != null && string.IsNullOrEmpty(obj.ToString()))
                {
                    return null;
                }
                else
                {
                    return Enum.Parse(underlyingType ?? type, obj.ToString());
                }
            }
            else if (typeof(IConvertible).IsAssignableFrom(underlyingType ?? type))
            {
                try
                {
                    if (obj is DateTime && type == typeof(string))
                    {
                        return ((DateTime)obj).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        return Convert.ChangeType(obj, underlyingType ?? type, null);
                    }
                }
                catch
                {
                    return underlyingType == null ? Activator.CreateInstance(type) : null;
                }
            }
            else
            {
                TypeConverter converter = TypeDescriptor.GetConverter(type);

                if (converter.CanConvertFrom(obj.GetType()))
                {
                    return converter.ConvertFrom(obj);
                }

                ConstructorInfo constructor = type.GetConstructor(Type.EmptyTypes);

                if (constructor != null)
                {
                    Type oldType = obj.GetType();

                    object o = constructor.Invoke(null);

                    IEnumerable data = obj as IEnumerable;

                    //泛型集合
                    if (data != null)
                    {
                        if (oldType.Name == "Dictionary`2")
                        {
                            var args = type.GetGenericArguments();

                            var type1 = args[0];

                            var type2 = args[1];

                            var cpv2 = (System.Collections.IDictionary)Activator.CreateInstance(type);

                            var nData = (System.Collections.IDictionary)data;

                            foreach (DictionaryEntry item in nData)
                            {
                                cpv2.Add(ConvertTo(item.Key, type1), ConvertTo(item.Value, type2));
                            }
                            o = cpv2;
                        }
                        else
                        {
                            foreach (var item in data)
                            {
                                var nItem = ConvertTo(item, type.GetGenericArguments()[0]);
                                type.GetMethod("Add").Invoke(o, new[] { nItem });
                            }
                        }
                    }
                    else
                    {
                        PropertyInfo[] propertys = type.GetProperties();

                        foreach (PropertyInfo property in propertys)
                        {
                            PropertyInfo p = oldType.GetProperty(property.Name);

                            if (property.CanWrite && p != null && p.CanRead)
                            {
                                if (p.PropertyType.IsAnsiClass && (p.PropertyType.IsGenericType || p.PropertyType.IsArray))
                                {
                                    if (p.PropertyType.Name == "Nullable`1")
                                    {
                                        var pv = p.GetValue(obj, null);

                                        if (pv != null)
                                        {
                                            var args = property.PropertyType.GetGenericArguments();

                                            var ptype = args[0];

                                            property.SetValue(o, ConvertTo(pv, ptype), null);
                                        }
                                    }
                                    else
                                    {
                                        if (p.PropertyType.Name == "Dictionary`2" || p.PropertyType.Name == "IDictionary`2")
                                        {
                                            var pv = p.GetValue(obj, null);

                                            if (pv != null)
                                            {
                                                var args = property.PropertyType.GetGenericArguments();

                                                var type1 = args[0];

                                                var type2 = args[1];

                                                var cpv = (System.Collections.IDictionary)pv;

                                                var cpv2 = (System.Collections.IDictionary)Activator.CreateInstance(property.PropertyType);

                                                foreach (DictionaryEntry item in cpv)
                                                {
                                                    cpv2.Add(ConvertTo(item.Key, type1), ConvertTo(item.Value, type2));
                                                }

                                                property.SetValue(o, cpv2, null);

                                            }
                                        }
                                        else
                                        {
                                            var pv = p.GetValue(obj, null);

                                            if (pv != null)
                                            {
                                                var args = property.PropertyType.GetGenericArguments();

                                                var ptype1 = args[0];

                                                var cpv = (System.Collections.IList)pv;

                                                var cpv2 = (System.Collections.IList)Activator.CreateInstance(property.PropertyType);

                                                foreach (var item in cpv)
                                                {
                                                    cpv2.Add(ConvertTo(item, ptype1));
                                                }

                                                property.SetValue(o, cpv2, null);
                                            }

                                        }
                                    }
                                }
                                else
                                {
                                    var pv = p.GetValue(obj, null);

                                    if (pv != null)

                                        property.SetValue(o, ConvertTo(pv, property.PropertyType), null);
                                }
                            }
                        }
                    }

                    return o;
                }
            }
            return obj;
        }


        /// <summary>
        /// 将obj转换成T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static T ConvertTo<T>(this object obj) where T : class, new()
        {
            if (obj == null) return default(T);
            return (T)obj.ConvertTo(typeof(T));
        }

        /// <summary>
        /// 复制到另一实体
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="t"></param>
        /// <returns></returns>
        public static T Clone<T>(this T t) where T : class, new()
        {
            return (T)ConvertTo(t, typeof(T));
        }
    }
}
