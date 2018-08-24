/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.Commom
*文件名： PropertyOperation
*版本号： V1.0.0.0
*唯一标识：7d55d6ef-ceb1-4f7a-8f00-cc381341a07f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/29 16:36:58
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/29 16:36:58
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace SAEA.Common
{
    /// <summary>
    /// 对象属于操作类
    /// </summary>
    public static class PropertyOperation
    {
        #region reflect

        /// <summary>
        /// 获取值
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public static object ReflectGetter(object obj, string propertyName)
        {
            var type = obj.GetType();
            var propertyInfo = type.GetProperty(propertyName);
            var propertyValue = propertyInfo.GetValue(obj);
            return propertyValue;
        }
        /// <summary>
        /// 赋值
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="propertyName"></param>
        /// <param name="propertyValue"></param>
        public static void ReflectSetter(object obj, string propertyName, object propertyValue)
        {
            var type = obj.GetType();
            var propertyInfo = type.GetProperty(propertyName);
            propertyInfo.SetValue(obj, propertyValue);
        }

        #endregion

        #region expression

        /// <summary>
        /// 表达式获取对象的属性值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public static Func<T, object> ExpresionGetter<T>(string propertyName)
        {
            var type = typeof(T);
            var property = type.GetProperty(propertyName);
            //// 对象实例
            var parameterExpression = Expression.Parameter(typeof(object), "obj");
            //// 转换参数为真实类型
            var unaryExpression = Expression.Convert(parameterExpression, type);
            //// 调用获取属性的方法
            var callMethod = Expression.Call(unaryExpression, property.GetGetMethod());
            var expression = Expression.Lambda<Func<T, object>>(callMethod, parameterExpression);
            return expression.Compile();
        }


        /// <summary>
        /// 表达式设置对象的属性值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public static Action<T, object> ExpresionSetter<T>(string propertyName)
        {
            var type = typeof(T);
            var property = type.GetProperty(propertyName);
            var objectParameterExpression = Expression.Parameter(typeof(object), "obj");
            var objectUnaryExpression = Expression.Convert(objectParameterExpression, type);
            var valueParameterExpression = Expression.Parameter(typeof(object), "val");
            var valueUnaryExpression = Expression.Convert(valueParameterExpression, property.PropertyType);
            //// 调用给属性赋值的方法
            var body = Expression.Call(objectUnaryExpression, property.GetSetMethod(), valueUnaryExpression);
            var expression = Expression.Lambda<Action<T, object>>(body, objectParameterExpression, valueParameterExpression);
            return expression.Compile();
        }
        
        #endregion

        #region Emit

        /// <summary>
        /// Emit获取对象的属性值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public static Func<T, object> EmitGetter<T>(string propertyName)
        {
            var type = typeof(T);
            var dynamicMethod = new DynamicMethod("get_" + propertyName, typeof(object), new[] { type }, type);
            var iLGenerator = dynamicMethod.GetILGenerator();
            iLGenerator.Emit(OpCodes.Ldarg_0);
            var property = type.GetProperty(propertyName);
            iLGenerator.Emit(OpCodes.Callvirt, property.GetMethod);
            if (property.PropertyType.IsValueType)
            {
                // 如果是值类型，装箱
                iLGenerator.Emit(OpCodes.Box, property.PropertyType);
            }
            else
            {
                // 如果是引用类型，转换
                iLGenerator.Emit(OpCodes.Castclass, property.PropertyType);
            }
            iLGenerator.Emit(OpCodes.Ret);
            return dynamicMethod.CreateDelegate(typeof(Func<T, object>)) as Func<T, object>;
        }       

        /// <summary>
        /// Emit设置对象的属性值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public static Action<object, object> EmitSetter(string propertyName)
        {
            var type = typeof(object);
            var dynamicMethod = new DynamicMethod("EmitCallable", null, new[] { type, typeof(object) }, type.Module);
            var iLGenerator = dynamicMethod.GetILGenerator();
            var callMethod = type.GetMethod("set_" + propertyName, BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.Public);
            var parameterInfo = callMethod.GetParameters()[0];
            var local = iLGenerator.DeclareLocal(parameterInfo.ParameterType, true);
            iLGenerator.Emit(OpCodes.Ldarg_1);
            if (parameterInfo.ParameterType.IsValueType)
            {
                // 如果是值类型，拆箱
                iLGenerator.Emit(OpCodes.Unbox_Any, parameterInfo.ParameterType);
            }
            else
            {
                // 如果是引用类型，转换
                iLGenerator.Emit(OpCodes.Castclass, parameterInfo.ParameterType);
            }
            iLGenerator.Emit(OpCodes.Stloc, local);
            iLGenerator.Emit(OpCodes.Ldarg_0);
            iLGenerator.Emit(OpCodes.Ldloc, local);
            iLGenerator.EmitCall(OpCodes.Callvirt, callMethod, null);
            iLGenerator.Emit(OpCodes.Ret);
            return dynamicMethod.CreateDelegate(typeof(Action<object, object>)) as Action<object, object>;
        }        
        #endregion
    }
}
