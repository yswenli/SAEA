/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Commom
*文件名： ModelCloneHelper
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

namespace SAEA.Common
{
    /// <summary>
    /// 实体复制工具类
    /// </summary>
    public static class ModelCloneHelper
    {
        /// <summary>
        /// 转换成另外一个实体
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static T ConvertTo<T>(this object source) where T : class
        {
            if (source != null && source.GetType().IsClass)
            {
                var sourceProperties = source.GetType().GetProperties();

                var type = typeof(T);

                var target = (T)Activator.CreateInstance(type);

                var targetProperties = type.GetProperties();

                foreach (var targetProperty in targetProperties)
                {

                    var sourceProperty = sourceProperties.Where(b => b.Name.ToLower() == targetProperty.Name.ToLower()).FirstOrDefault();

                    if (sourceProperty != null)
                    {
                        try
                        {
                            var val = sourceProperty.GetValue(source, null);

                            if (sourceProperty.PropertyType == targetProperty.PropertyType)
                            {
                                if (val != null)
                                {
                                    targetProperty.SetValue(target, val, null);
                                }
                            }
                            else
                            {
                                //不同类型转换

                                #region 日期

                                if (sourceProperty.PropertyType == typeof(DateTime))
                                {
                                    if (targetProperty.PropertyType == typeof(string))
                                    {
                                        if (val != null)
                                        {
                                            var dt = (DateTime)val;
                                            targetProperty.SetValue(target, dt.ToString("yyyy-MM-dd HH:mm:ss"), null);
                                        }
                                    }
                                    else if (targetProperty.PropertyType == typeof(Nullable<DateTime>))
                                    {
                                        if (val != null)
                                        {
                                            targetProperty.SetValue(target, val, null);
                                        }
                                    }
                                }
                                else if (sourceProperty.PropertyType == typeof(Nullable<DateTime>))
                                {
                                    if (targetProperty.PropertyType == typeof(string))
                                    {
                                        if (val != null)
                                        {
                                            var dt = (Nullable<DateTime>)val;
                                            if (dt.HasValue)
                                            {
                                                targetProperty.SetValue(target, dt.Value.ToString("yyyy-MM-dd HH:mm:ss"), null);
                                            }
                                        }
                                    }
                                    else if (targetProperty.PropertyType == typeof(DateTime))
                                    {
                                        if (val != null)
                                        {
                                            var dt = (Nullable<DateTime>)val;
                                            if (dt.HasValue)
                                            {
                                                targetProperty.SetValue(target, dt.Value, null);
                                            }
                                        }
                                    }
                                }

                                #endregion

                                #region 字符串

                                if (sourceProperty.PropertyType == typeof(string))
                                {
                                    var str = (string)val;

                                    if (targetProperty.PropertyType == typeof(int) || targetProperty.PropertyType == typeof(Nullable<int>))
                                    {
                                        var num = 0;

                                        if (!string.IsNullOrWhiteSpace(str))
                                        {
                                            int.TryParse(str, out num);
                                        }

                                        targetProperty.SetValue(target, num, null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(long) || targetProperty.PropertyType == typeof(Nullable<long>))
                                    {
                                        long num = 0;

                                        if (!string.IsNullOrWhiteSpace(str))
                                        {
                                            long.TryParse(str, out num);
                                        }

                                        targetProperty.SetValue(target, num, null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(short) || targetProperty.PropertyType == typeof(Nullable<short>))
                                    {
                                        short num = 0;

                                        if (!string.IsNullOrWhiteSpace(str))
                                        {
                                            short.TryParse(str, out num);
                                        }

                                        targetProperty.SetValue(target, num, null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(byte) || targetProperty.PropertyType == typeof(Nullable<byte>))
                                    {
                                        byte num = 0;

                                        if (!string.IsNullOrWhiteSpace(str))
                                        {
                                            byte.TryParse(str, out num);
                                        }

                                        targetProperty.SetValue(target, num, null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(float) || targetProperty.PropertyType == typeof(Nullable<float>))
                                    {
                                        float num = 0;

                                        if (!string.IsNullOrWhiteSpace(str))
                                        {
                                            float.TryParse(str, out num);
                                        }

                                        targetProperty.SetValue(target, num, null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(double) || targetProperty.PropertyType == typeof(Nullable<double>))
                                    {
                                        double num = 0;

                                        if (!string.IsNullOrWhiteSpace(str))
                                        {
                                            double.TryParse(str, out num);
                                        }

                                        targetProperty.SetValue(target, num, null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(decimal) || targetProperty.PropertyType == typeof(Nullable<decimal>))
                                    {
                                        decimal num = 0;

                                        if (!string.IsNullOrWhiteSpace(str))
                                        {
                                            decimal.TryParse(str, out num);
                                        }

                                        targetProperty.SetValue(target, num, null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(bool) || targetProperty.PropertyType == typeof(Nullable<bool>))
                                    {
                                        var bVal = false;

                                        if (!string.IsNullOrWhiteSpace(str))
                                        {
                                            bool.TryParse(str, out bVal);
                                        }

                                        if (str != "0" && str != "false")
                                        {
                                            bVal = true;
                                        }

                                        targetProperty.SetValue(target, bVal, null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(DateTime) || targetProperty.PropertyType == typeof(Nullable<DateTime>))
                                    {
                                        if (!string.IsNullOrWhiteSpace(str))
                                        {
                                            if (DateTime.TryParse(str, out DateTime dt))
                                            {
                                                targetProperty.SetValue(target, dt, null);
                                            }
                                        }
                                    }
                                }

                                #endregion

                                #region 数字

                                if (sourceProperty.PropertyType == typeof(byte))
                                {
                                    if (targetProperty.PropertyType == typeof(string))
                                    {
                                        targetProperty.SetValue(target, val.ToString(), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(Nullable<byte>))
                                    {
                                        targetProperty.SetValue(target, val, null);
                                    }
                                    else if (targetProperty.PropertyType.IsEnum)
                                    {
                                        var eVal = Enum.Parse(targetProperty.PropertyType, val.ToString());

                                        targetProperty.SetValue(target, eVal, null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(bool) || targetProperty.PropertyType == typeof(Nullable<bool>))
                                    {
                                        targetProperty.SetValue(target, (Convert.ToByte(val)) != 0, null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(short) || targetProperty.PropertyType == typeof(Nullable<short>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToInt16(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(int) || targetProperty.PropertyType == typeof(Nullable<int>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToInt32(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(decimal) || targetProperty.PropertyType == typeof(Nullable<decimal>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToDecimal(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(float) || targetProperty.PropertyType == typeof(Nullable<float>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToSingle(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(double) || targetProperty.PropertyType == typeof(Nullable<double>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToDouble(val), null);
                                    }
                                }
                                else if (sourceProperty.PropertyType == typeof(Nullable<byte>))
                                {
                                    if (val != null)
                                    {
                                        var nVal = (Nullable<byte>)val;

                                        if (nVal.HasValue)
                                        {
                                            if (targetProperty.PropertyType == typeof(string))
                                            {
                                                targetProperty.SetValue(target, nVal.Value.ToString(), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(byte))
                                            {
                                                targetProperty.SetValue(target, nVal, null);
                                            }
                                            else if (targetProperty.PropertyType.IsEnum)
                                            {
                                                var eVal = Enum.Parse(targetProperty.PropertyType, nVal.Value.ToString());

                                                targetProperty.SetValue(target, eVal, null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(bool) || targetProperty.PropertyType == typeof(Nullable<bool>))
                                            {
                                                targetProperty.SetValue(target, (Convert.ToByte(nVal.Value)) != 0, null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(short) || targetProperty.PropertyType == typeof(Nullable<short>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToInt16(nVal.Value), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(int) || targetProperty.PropertyType == typeof(Nullable<int>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToInt32(nVal.Value), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(decimal) || targetProperty.PropertyType == typeof(Nullable<decimal>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToDecimal(nVal.Value), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(long) || targetProperty.PropertyType == typeof(Nullable<long>))
                                            {
                                                targetProperty.SetValue(target, (long)nVal.Value, null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(float) || targetProperty.PropertyType == typeof(Nullable<float>))
                                            {
                                                targetProperty.SetValue(target, (float)nVal.Value, null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(double) || targetProperty.PropertyType == typeof(Nullable<double>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToDouble(nVal.Value), null);
                                            }
                                        }
                                    }
                                }

                                if (sourceProperty.PropertyType == typeof(short))
                                {
                                    if (targetProperty.PropertyType == typeof(string))
                                    {
                                        targetProperty.SetValue(target, val.ToString(), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(Nullable<short>))
                                    {
                                        targetProperty.SetValue(target, val, null);
                                    }
                                    else if (targetProperty.PropertyType.IsEnum)
                                    {
                                        var eVal = Enum.Parse(targetProperty.PropertyType, val.ToString());

                                        targetProperty.SetValue(target, eVal, null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(bool) || targetProperty.PropertyType == typeof(Nullable<bool>))
                                    {
                                        targetProperty.SetValue(target, (Convert.ToInt16(val)) != 0, null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(byte) || targetProperty.PropertyType == typeof(Nullable<byte>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToByte(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(int) || targetProperty.PropertyType == typeof(Nullable<int>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToInt32(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(decimal) || targetProperty.PropertyType == typeof(Nullable<decimal>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToDecimal(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(float) || targetProperty.PropertyType == typeof(Nullable<float>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToSingle(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(double) || targetProperty.PropertyType == typeof(Nullable<double>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToDouble(val), null);
                                    }
                                }
                                else if (sourceProperty.PropertyType == typeof(Nullable<short>))
                                {
                                    if (val != null)
                                    {
                                        var nVal = (Nullable<short>)val;

                                        if (nVal.HasValue)
                                        {
                                            if (targetProperty.PropertyType == typeof(string))
                                            {
                                                targetProperty.SetValue(target, nVal.Value.ToString(), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(short))
                                            {
                                                targetProperty.SetValue(target, nVal.Value, null);
                                            }
                                            else if (targetProperty.PropertyType.IsEnum)
                                            {
                                                var eVal = Enum.Parse(targetProperty.PropertyType, nVal.Value.ToString());

                                                targetProperty.SetValue(target, eVal, null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(bool) || targetProperty.PropertyType == typeof(Nullable<bool>))
                                            {
                                                targetProperty.SetValue(target, (Convert.ToInt16(nVal.Value)) != 0, null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(byte) || targetProperty.PropertyType == typeof(Nullable<byte>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToByte(nVal.Value), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(int) || targetProperty.PropertyType == typeof(Nullable<int>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToInt32(nVal.Value), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(decimal) || targetProperty.PropertyType == typeof(Nullable<decimal>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToDecimal(nVal.Value), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(long) || targetProperty.PropertyType == typeof(Nullable<long>))
                                            {
                                                targetProperty.SetValue(target, (long)nVal.Value, null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(float) || targetProperty.PropertyType == typeof(Nullable<float>))
                                            {
                                                targetProperty.SetValue(target, (float)nVal.Value, null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(double) || targetProperty.PropertyType == typeof(Nullable<double>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToDouble(nVal.Value), null);
                                            }
                                        }
                                    }
                                }

                                if (sourceProperty.PropertyType == typeof(int))
                                {
                                    if (targetProperty.PropertyType == typeof(string))
                                    {
                                        targetProperty.SetValue(target, val.ToString(), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(Nullable<int>))
                                    {
                                        targetProperty.SetValue(target, val, null);
                                    }
                                    else if (targetProperty.PropertyType.IsEnum)
                                    {
                                        var eVal = Enum.Parse(targetProperty.PropertyType, val.ToString());

                                        targetProperty.SetValue(target, eVal, null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(bool) || targetProperty.PropertyType == typeof(Nullable<bool>))
                                    {
                                        targetProperty.SetValue(target, (Convert.ToInt32(val)) != 0, null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(byte) || targetProperty.PropertyType == typeof(Nullable<byte>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToByte(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(short) || targetProperty.PropertyType == typeof(Nullable<short>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToInt16(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(decimal) || targetProperty.PropertyType == typeof(Nullable<decimal>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToDecimal(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(float) || targetProperty.PropertyType == typeof(Nullable<float>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToSingle(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(double) || targetProperty.PropertyType == typeof(Nullable<double>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToDouble(val), null);
                                    }
                                }
                                else if (sourceProperty.PropertyType == typeof(Nullable<int>))
                                {
                                    if (val != null)
                                    {
                                        var nVal = (Nullable<int>)val;

                                        if (nVal.HasValue)
                                        {
                                            if (targetProperty.PropertyType == typeof(string))
                                            {
                                                targetProperty.SetValue(target, nVal.Value.ToString(), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(int))
                                            {
                                                targetProperty.SetValue(target, nVal.Value, null);
                                            }
                                            else if (targetProperty.PropertyType.IsEnum)
                                            {
                                                var eVal = Enum.Parse(targetProperty.PropertyType, nVal.Value.ToString());

                                                targetProperty.SetValue(target, eVal, null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(bool) || targetProperty.PropertyType == typeof(Nullable<bool>))
                                            {
                                                targetProperty.SetValue(target, (Convert.ToInt32(nVal.Value)) != 0, null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(byte) || targetProperty.PropertyType == typeof(Nullable<byte>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToByte(nVal.Value), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(short) || targetProperty.PropertyType == typeof(Nullable<short>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToInt16(nVal.Value), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(decimal) || targetProperty.PropertyType == typeof(Nullable<decimal>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToDecimal(nVal.Value), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(long) || targetProperty.PropertyType == typeof(Nullable<long>))
                                            {
                                                targetProperty.SetValue(target, (long)nVal.Value, null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(float) || targetProperty.PropertyType == typeof(Nullable<float>))
                                            {
                                                targetProperty.SetValue(target, (float)nVal.Value, null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(double) || targetProperty.PropertyType == typeof(Nullable<double>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToDouble(nVal.Value), null);
                                            }
                                        }
                                    }
                                }

                                if (sourceProperty.PropertyType == typeof(long))
                                {
                                    if (targetProperty.PropertyType == typeof(string))
                                    {
                                        targetProperty.SetValue(target, val.ToString(), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(Nullable<long>))
                                    {
                                        targetProperty.SetValue(target, val, null);
                                    }
                                    else if (targetProperty.PropertyType.IsEnum)
                                    {
                                        var eVal = Enum.Parse(targetProperty.PropertyType, val.ToString());

                                        targetProperty.SetValue(target, eVal, null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(byte) || targetProperty.PropertyType == typeof(Nullable<byte>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToByte(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(short) || targetProperty.PropertyType == typeof(Nullable<short>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToInt16(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(int) || targetProperty.PropertyType == typeof(Nullable<int>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToInt32(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(decimal) || targetProperty.PropertyType == typeof(Nullable<decimal>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToDecimal(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(float) || targetProperty.PropertyType == typeof(Nullable<float>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToSingle(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(double) || targetProperty.PropertyType == typeof(Nullable<double>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToDouble(val), null);
                                    }
                                }
                                else if (sourceProperty.PropertyType == typeof(Nullable<long>))
                                {
                                    if (val != null)
                                    {
                                        var nVal = (Nullable<long>)val;

                                        if (nVal.HasValue)
                                        {
                                            if (targetProperty.PropertyType == typeof(string))
                                            {
                                                targetProperty.SetValue(target, nVal.Value.ToString(), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(long))
                                            {
                                                targetProperty.SetValue(target, nVal.Value, null);
                                            }
                                            else if (targetProperty.PropertyType.IsEnum)
                                            {
                                                var eVal = Enum.Parse(targetProperty.PropertyType, nVal.Value.ToString());

                                                targetProperty.SetValue(target, eVal, null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(byte) || targetProperty.PropertyType == typeof(Nullable<byte>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToByte(nVal.Value), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(short) || targetProperty.PropertyType == typeof(Nullable<short>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToInt16(nVal.Value), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(int) || targetProperty.PropertyType == typeof(Nullable<int>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToInt32(nVal.Value), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(double) || targetProperty.PropertyType == typeof(Nullable<double>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToDouble(nVal.Value), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(decimal) || targetProperty.PropertyType == typeof(Nullable<decimal>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToDecimal(nVal.Value), null);
                                            }
                                        }
                                    }
                                }


                                if (sourceProperty.PropertyType == typeof(float))
                                {
                                    if (targetProperty.PropertyType == typeof(string))
                                    {
                                        targetProperty.SetValue(target, val.ToString(), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(byte) || targetProperty.PropertyType == typeof(Nullable<byte>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToByte(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(short) || targetProperty.PropertyType == typeof(Nullable<short>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToInt16(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(int) || targetProperty.PropertyType == typeof(Nullable<int>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToInt32(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(long) || targetProperty.PropertyType == typeof(Nullable<long>))
                                    {
                                        targetProperty.SetValue(target, (long)val, null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(double) || targetProperty.PropertyType == typeof(Nullable<double>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToSingle(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(decimal) || targetProperty.PropertyType == typeof(Nullable<decimal>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToDecimal(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(Nullable<float>))
                                    {
                                        targetProperty.SetValue(target, val, null);
                                    }
                                }
                                else if (sourceProperty.PropertyType == typeof(Nullable<float>))
                                {
                                    if (val != null)
                                    {
                                        var nVal = (Nullable<float>)val;

                                        if (nVal.HasValue)
                                        {
                                            if (targetProperty.PropertyType == typeof(string))
                                            {
                                                targetProperty.SetValue(target, nVal.Value.ToString(), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(float))
                                            {
                                                targetProperty.SetValue(target, nVal.Value, null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(byte) || targetProperty.PropertyType == typeof(Nullable<byte>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToByte(nVal.Value), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(short) || targetProperty.PropertyType == typeof(Nullable<short>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToInt16(nVal.Value), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(int) || targetProperty.PropertyType == typeof(Nullable<int>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToInt32(nVal.Value), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(long) || targetProperty.PropertyType == typeof(Nullable<long>))
                                            {
                                                targetProperty.SetValue(target, (long)nVal.Value, null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(decimal) || targetProperty.PropertyType == typeof(Nullable<decimal>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToDecimal(nVal.Value), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(double) || targetProperty.PropertyType == typeof(Nullable<double>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToDouble(nVal.Value), null);
                                            }
                                        }
                                    }
                                }

                                if (sourceProperty.PropertyType == typeof(double))
                                {
                                    if (targetProperty.PropertyType == typeof(string))
                                    {
                                        targetProperty.SetValue(target, val.ToString(), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(byte) || targetProperty.PropertyType == typeof(Nullable<byte>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToByte(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(short) || targetProperty.PropertyType == typeof(Nullable<short>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToInt16(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(int) || targetProperty.PropertyType == typeof(Nullable<int>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToInt32(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(long) || targetProperty.PropertyType == typeof(Nullable<long>))
                                    {
                                        targetProperty.SetValue(target, (long)val, null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(float) || targetProperty.PropertyType == typeof(Nullable<float>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToSingle(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(decimal) || targetProperty.PropertyType == typeof(Nullable<decimal>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToDecimal(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(Nullable<double>))
                                    {
                                        targetProperty.SetValue(target, val, null);
                                    }
                                }
                                else if (sourceProperty.PropertyType == typeof(Nullable<double>))
                                {
                                    if (val != null)
                                    {
                                        var nVal = (Nullable<double>)val;

                                        if (nVal.HasValue)
                                        {
                                            if (targetProperty.PropertyType == typeof(string))
                                            {
                                                targetProperty.SetValue(target, nVal.Value.ToString(), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(decimal) || targetProperty.PropertyType == typeof(Nullable<decimal>))
                                            {
                                                targetProperty.SetValue(target, nVal.Value, null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(byte) || targetProperty.PropertyType == typeof(Nullable<byte>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToByte(nVal.Value), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(short) || targetProperty.PropertyType == typeof(Nullable<short>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToInt16(nVal.Value), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(int) || targetProperty.PropertyType == typeof(Nullable<int>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToInt32(nVal.Value), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(long) || targetProperty.PropertyType == typeof(Nullable<long>))
                                            {
                                                targetProperty.SetValue(target, (long)nVal.Value, null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(float) || targetProperty.PropertyType == typeof(Nullable<float>))
                                            {
                                                targetProperty.SetValue(target, (float)nVal.Value, null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(double))
                                            {
                                                targetProperty.SetValue(target, Convert.ToDouble(nVal.Value), null);
                                            }
                                        }
                                    }
                                }

                                if (sourceProperty.PropertyType == typeof(decimal))
                                {
                                    if (targetProperty.PropertyType == typeof(string))
                                    {
                                        targetProperty.SetValue(target, val.ToString(), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(byte) || targetProperty.PropertyType == typeof(Nullable<byte>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToByte(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(short) || targetProperty.PropertyType == typeof(Nullable<short>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToInt16(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(int) || targetProperty.PropertyType == typeof(Nullable<int>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToInt32(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(long) || targetProperty.PropertyType == typeof(Nullable<long>))
                                    {
                                        targetProperty.SetValue(target, (long)val, null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(float) || targetProperty.PropertyType == typeof(Nullable<float>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToSingle(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(double) || targetProperty.PropertyType == typeof(Nullable<double>))
                                    {
                                        targetProperty.SetValue(target, Convert.ToDouble(val), null);
                                    }
                                    else if (targetProperty.PropertyType == typeof(Nullable<decimal>))
                                    {
                                        targetProperty.SetValue(target, val, null);
                                    }
                                }
                                else if (sourceProperty.PropertyType == typeof(Nullable<decimal>))
                                {
                                    if (val != null)
                                    {
                                        var nVal = (Nullable<decimal>)val;

                                        if (nVal.HasValue)
                                        {
                                            if (targetProperty.PropertyType == typeof(string))
                                            {
                                                targetProperty.SetValue(target, nVal.Value.ToString(), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(decimal))
                                            {
                                                targetProperty.SetValue(target, nVal.Value, null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(byte) || targetProperty.PropertyType == typeof(Nullable<byte>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToByte(nVal.Value), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(short) || targetProperty.PropertyType == typeof(Nullable<short>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToInt16(nVal.Value), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(int) || targetProperty.PropertyType == typeof(Nullable<int>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToInt32(nVal.Value), null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(long) || targetProperty.PropertyType == typeof(Nullable<long>))
                                            {
                                                targetProperty.SetValue(target, (long)nVal.Value, null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(float) || targetProperty.PropertyType == typeof(Nullable<float>))
                                            {
                                                targetProperty.SetValue(target, (float)nVal.Value, null);
                                            }
                                            else if (targetProperty.PropertyType == typeof(double) || targetProperty.PropertyType == typeof(Nullable<double>))
                                            {
                                                targetProperty.SetValue(target, Convert.ToDouble(nVal.Value), null);
                                            }
                                        }
                                    }
                                }

                                #endregion

                                #region 枚举

                                if (sourceProperty.PropertyType.IsEnum)
                                {
                                    if (sourceProperty.PropertyType == typeof(string))
                                    {
                                        var str = Enum.GetName(val.GetType(), val);
                                        targetProperty.SetValue(target, str, null);
                                    }
                                    else if (sourceProperty.PropertyType == typeof(byte))
                                    {
                                        var nVal = Convert.ToByte(val);
                                        targetProperty.SetValue(target, nVal, null);
                                    }
                                    else if (sourceProperty.PropertyType == typeof(short))
                                    {
                                        var nVal = Convert.ToInt16(val);
                                        targetProperty.SetValue(target, nVal, null);
                                    }
                                    else if (sourceProperty.PropertyType == typeof(int))
                                    {
                                        var nVal = Convert.ToInt32(val);
                                        targetProperty.SetValue(target, nVal, null);
                                    }
                                }
                                #endregion
                            }
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"WEF ConvertTo 中指定的转换无效，sourceProperty:{sourceProperty.Name} {sourceProperty.PropertyType}, targetProperty:{targetProperty.Name} {targetProperty.PropertyType} ,err:{ex.Message}");
                        }
                    }


                }
                return target;
            }
            return default(T);
        }

        /// <summary>
        /// 转换成另外一个实体列表
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static List<T> ConvertToList<T>(this object source) where T : class
        {
            if (source != null)
            {
                if (source.GetType().GetInterface("IEnumerable", true) != null)
                {
                    var list = (System.Collections.IEnumerable)source;

                    var result = new List<T>();

                    foreach (var item in list)
                    {
                        result.Add(item.ConvertTo<T>());
                    }

                    return result;
                }
            }
            return null;
        }
    }
}
