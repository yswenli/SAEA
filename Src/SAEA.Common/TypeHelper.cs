/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Commom
*文件名： TypeHelper
*版本号： v6.0.0.1
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
*版本号： v6.0.0.1
*描述：
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
        public static readonly string[] ListTypeStrs = { "List`1", "HashSet`1", "IList`1", "ISet`1", "ICollection`1", "IEnumerable`1" };

        public static readonly string[] DicTypeStrs = { "Dictionary`2", "IDictionary`2" };

        static readonly StackTrace StackTrace = new StackTrace(true);
        
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
        /// 添加或获取实例
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
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

    public class TypeInfo
    {
        public Type Type { get; set; }

        public Object Instance { get; set; }

        public Type[] ArgTypes { get; set; }

        public MethodInfo MethodInfo { get; set; }
    }
}
