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
*命名空间：SAEA.Common
*文件名： TypeHelper
*版本号： v26.4.23.1
*唯一标识：33689418-1035-4133-b7b6-a972ceb53148
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/08/24 16:31:11
*描述：TypeHelper帮助类
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace SAEA.Common
{
    /// <summary>
    /// 程序集获取方法
    /// </summary>
    public static class TypeHelper
    {
        /// <summary>
        /// 泛型列表类型名称集合，用于反射时识别列表类类型
        /// </summary>
        public static readonly string[] ListTypeStrs = { "List`1", "HashSet`1", "IList`1", "ISet`1", "ICollection`1", "IEnumerable`1" };

        /// <summary>
        /// 泛型字典类型名称集合，用于反射时识别字典类类型
        /// </summary>
        public static readonly string[] DicTypeStrs = { "Dictionary`2", "IDictionary`2" };

        static readonly StackTrace StackTrace = new StackTrace(true);
        
        /// <summary>
        /// 从类型初始化时抓取的调用栈中查找 Generate 或 Start 方法的下一帧程序集，返回其全部类型
        /// </summary>
        /// <returns>程序集中的全部类型，未找到匹配帧时返回 null</returns>
        public static Type[] GetDefalt()
        {
            var frames = StackTrace.GetFrames();

            MethodBase[] mbs = new MethodBase[frames.Length];

            for (int i = 0; i < frames.Length; i++)
            {
                mbs[i] = frames[i].GetMethod();
                if (mbs[i].Name.Equals("Generate") || mbs[i].Name.Equals("Start"))
                {
                    return frames[i + 1].GetMethod().DeclaringType.Assembly.GetTypes();
                }
            }

            return null;
        }

        /// <summary>
        /// 获取类型名称
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string GetTypeName(Type type)
        {
            if (type.IsClass || type.IsInterface)
            {
                if (type.IsGenericType)
                {
                    var tName = type.Name.Substring(0, type.Name.Length - 2) + "<";

                    var gArgs = type.GetGenericArguments();

                    for (int i = 0; i < gArgs.Length; i++)
                    {
                        if (i == gArgs.Length - 1)
                        {
                            tName += GetTypeName(gArgs[i]);
                        }
                        else
                        {
                            tName += GetTypeName(gArgs[i]) + ",";
                        }
                    }
                    tName += ">";
                    return tName;
                }
            }
            return type.Name;
        }

        private static readonly object SyncRoot = new object();

        private static readonly ConcurrentDictionary<string, TypeInfo> InstanceCache = new ConcurrentDictionary<string, TypeInfo>();


        /// <summary>
        /// 添加或获取实例，按类型全名与方法名缓存类型信息，实例每次调用都重新创建
        /// </summary>
        /// <param name="type">目标类型，接口类型会抛出异常</param>
        /// <param name="methodName">要缓存的方法名，默认 Add</param>
        /// <returns>类型信息，类型不是类时返回 null</returns>
        public static TypeInfo GetOrAddInstance(Type type, string methodName = "Add")
        {
            if (type.IsInterface)
            {
                throw new Exception("服务方法中不能包含接口内容！");
            }

            if (type.IsClass)
            {
                var fullName = type.FullName + methodName;

                TypeInfo typeInfo = InstanceCache.GetOrAdd(fullName, (v) =>
                {
                    Type[] argsTypes = null;

                    if (type.IsGenericType)
                    {
                        argsTypes = type.GetGenericArguments();
                        type = type.GetGenericTypeDefinition().MakeGenericType(argsTypes);
                    }

                    var mi = type.GetMethod(methodName);

                    return new TypeInfo()
                    {
                        Type = type,
                        MethodInfo = mi,
                        ArgTypes = argsTypes
                    };
                });
                typeInfo.Instance = Activator.CreateInstance(type);
                return typeInfo;
            }
            return null;
        }

        /// <summary>
        /// 按指定方法信息获取类型信息，串行加锁访问缓存，接口类型会抛出异常
        /// </summary>
        /// <param name="type">目标类型</param>
        /// <param name="mb">要缓存的方法信息</param>
        /// <returns>类型信息，实例每次调用都重新创建</returns>
        public static TypeInfo GetOrAddInstance(Type type, MethodInfo mb)
        {
            lock (SyncRoot)
            {
                if (type.IsInterface)
                {
                    throw new Exception("服务方法中不能包含接口内容！");
                }

                var fullName = type.FullName + mb.Name;

                TypeInfo typeInfo = InstanceCache.GetOrAdd(fullName, (v) =>
                {
                    return new TypeInfo()
                    {
                        Type = type,
                        MethodInfo = mb
                    };
                });
                typeInfo.Instance = Activator.CreateInstance(type);
                return typeInfo;
            }
        }
    }

    /// <summary>
    /// 类型实例缓存信息
    /// </summary>
    public class TypeInfo
    {
        /// <summary>
        /// 目标类型
        /// </summary>
        public Type Type { get; set; }

        /// <summary>
        /// 类型实例，每次获取时重新创建
        /// </summary>
        public Object Instance { get; set; }

        /// <summary>
        /// 泛型参数类型，非泛型类型为 null
        /// </summary>
        public Type[] ArgTypes { get; set; }

        /// <summary>
        /// 缓存的方法信息
        /// </summary>
        public MethodInfo MethodInfo { get; set; }
    }
}