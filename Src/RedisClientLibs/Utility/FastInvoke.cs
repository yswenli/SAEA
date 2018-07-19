/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.Commom
*文件名： FastInvoke
*版本号： V1.0.0.0
*唯一标识：045fba7e-77f2-416f-be74-4c91bf634110
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/30 16:13:26
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/30 16:13:26
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace SAEA.Common
{
    /// <summary>
    /// 快速对反射方法进行Invoke
    /// </summary>
    public static class FastInvoke
    {
        /// <summary>
        /// 方法执行代理
        /// </summary>
        /// <param name="target"></param>
        /// <param name="paramters"></param>
        /// <returns></returns>
        public delegate object FastInvokeHandler(object target, params object[] paramters);

        static object InvokeMethod(FastInvokeHandler invoke, object target, params object[] paramters)
        {
            return invoke(target, paramters);
        }

        /// <summary>
        /// 获取方法执行代理
        /// </summary>
        /// <param name="methodInfo"></param>
        /// <returns></returns>
        public static FastInvokeHandler GetMethodInvoker(MethodInfo methodInfo)
        {
            DynamicMethod dynamicMethod = new DynamicMethod(string.Empty, typeof(object), new Type[] { typeof(object), typeof(object[]) }, methodInfo.DeclaringType.Module);
            ILGenerator il = dynamicMethod.GetILGenerator();
            ParameterInfo[] ps = methodInfo.GetParameters();
            Type[] paramTypes = new Type[ps.Length];
            for (int i = 0; i < paramTypes.Length; i++)
            {
                if (ps[i].ParameterType.IsByRef)
                    paramTypes[i] = ps[i].ParameterType.GetElementType();
                else
                    paramTypes[i] = ps[i].ParameterType;
            }
            LocalBuilder[] locals = new LocalBuilder[paramTypes.Length];

            for (int i = 0; i < paramTypes.Length; i++)
            {
                locals[i] = il.DeclareLocal(paramTypes[i], true);
            }
            for (int i = 0; i < paramTypes.Length; i++)
            {
                il.Emit(OpCodes.Ldarg_1);
                EmitFastInt(il, i);
                il.Emit(OpCodes.Ldelem_Ref);
                EmitCastToReference(il, paramTypes[i]);
                il.Emit(OpCodes.Stloc, locals[i]);
            }
            if (!methodInfo.IsStatic)
            {
                il.Emit(OpCodes.Ldarg_0);
            }
            for (int i = 0; i < paramTypes.Length; i++)
            {
                if (ps[i].ParameterType.IsByRef)
                    il.Emit(OpCodes.Ldloca_S, locals[i]);
                else
                    il.Emit(OpCodes.Ldloc, locals[i]);
            }
            if (methodInfo.IsStatic)
                il.EmitCall(OpCodes.Call, methodInfo, null);
            else
                il.EmitCall(OpCodes.Callvirt, methodInfo, null);
            if (methodInfo.ReturnType == typeof(void))
                il.Emit(OpCodes.Ldnull);
            else
                EmitBoxIfNeeded(il, methodInfo.ReturnType);

            for (int i = 0; i < paramTypes.Length; i++)
            {
                if (ps[i].ParameterType.IsByRef)
                {
                    il.Emit(OpCodes.Ldarg_1);
                    EmitFastInt(il, i);
                    il.Emit(OpCodes.Ldloc, locals[i]);
                    if (locals[i].LocalType.IsValueType)
                        il.Emit(OpCodes.Box, locals[i].LocalType);
                    il.Emit(OpCodes.Stelem_Ref);
                }
            }

            il.Emit(OpCodes.Ret);
            FastInvokeHandler invoder = (FastInvokeHandler)dynamicMethod.CreateDelegate(typeof(FastInvokeHandler));
            return invoder;
        }

        private static void EmitCastToReference(ILGenerator il, System.Type type)
        {
            if (type.IsValueType)
            {
                il.Emit(OpCodes.Unbox_Any, type);
            }
            else
            {
                il.Emit(OpCodes.Castclass, type);
            }
        }

        private static void EmitBoxIfNeeded(ILGenerator il, System.Type type)
        {
            if (type.IsValueType)
            {
                il.Emit(OpCodes.Box, type);
            }
        }

        private static void EmitFastInt(ILGenerator il, int value)
        {
            switch (value)
            {
                case -1:
                    il.Emit(OpCodes.Ldc_I4_M1);
                    return;
                case 0:
                    il.Emit(OpCodes.Ldc_I4_0);
                    return;
                case 1:
                    il.Emit(OpCodes.Ldc_I4_1);
                    return;
                case 2:
                    il.Emit(OpCodes.Ldc_I4_2);
                    return;
                case 3:
                    il.Emit(OpCodes.Ldc_I4_3);
                    return;
                case 4:
                    il.Emit(OpCodes.Ldc_I4_4);
                    return;
                case 5:
                    il.Emit(OpCodes.Ldc_I4_5);
                    return;
                case 6:
                    il.Emit(OpCodes.Ldc_I4_6);
                    return;
                case 7:
                    il.Emit(OpCodes.Ldc_I4_7);
                    return;
                case 8:
                    il.Emit(OpCodes.Ldc_I4_8);
                    return;
            }

            if (value > -129 && value < 128)
            {
                il.Emit(OpCodes.Ldc_I4_S, (SByte)value);
            }
            else
            {
                il.Emit(OpCodes.Ldc_I4, value);
            }
        }


        #region Property

        private static object _syncRoot = new object();

        private static Dictionary<string, FastInvokeHandler> _invokeInfos = new Dictionary<string, FastInvokeHandler>();

        /// <summary>
        /// 属性赋值
        /// </summary>
        /// <param name="type"></param>
        /// <param name="obj"></param>
        /// <param name="property"></param>
        /// <param name="val"></param>
        public static void Setter(Type type, Object obj, PropertyInfo property, object val)
        {
            lock (_syncRoot)
            {
                var name = type.FullName + property.Name + "Setter";

                if (_invokeInfos.ContainsKey(name))
                {
                    _invokeInfos[name].Invoke(obj, val);
                }
                else
                {
                    var del = FastInvoke.GetMethodInvoker(property.GetSetMethod(true));
                    _invokeInfos.Add(name, del);
                    del.Invoke(obj, val);
                }
            }
        }
        /// <summary>
        /// 属性取值
        /// </summary>
        /// <param name="type"></param>
        /// <param name="obj"></param>
        /// <param name="property"></param>
        /// <returns></returns>

        public static object Getter(Type type, Object obj, PropertyInfo property)
        {
            lock (_syncRoot)
            {
                var name = type.FullName + property.Name + "Getter";

                if (_invokeInfos.ContainsKey(name))
                {
                    return _invokeInfos[name].Invoke(obj);
                }
                else
                {
                    var del = FastInvoke.GetMethodInvoker(property.GetGetMethod(true));
                    _invokeInfos.Add(name, del);
                    return del.Invoke(obj);
                }
            }
        }

        #endregion

        /// <summary>
        /// 将List~object转换成List~T
        /// </summary>
        /// <param name="params"></param>
        /// <returns></returns>
        public static object ToGList(this List<object> @params)
        {
            var type = @params.GetType();

            var stype = type.GenericTypeArguments[0];

            var gtype = type.GetGenericTypeDefinition().MakeGenericType(stype);

            var result = Activator.CreateInstance(gtype);

            var addMethod = gtype.GetMethod("AddRange");

            var methodInvoker = FastInvoke.GetMethodInvoker(addMethod);

            methodInvoker.Invoke(result, @params.ToArray());

            return result;
        }
    }
}
