using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace SAEA.Common
{
    /// <summary>
    /// 程序集获取方法
    /// 因为.net framework的
    /// </summary>
    public static class TypeHelper
    {
        public static readonly string[] ListTypeStrs = { "List`1", "HashSet`1", "IList`1", "ISet`1", "ICollection`1", "IEnumerable`1" };

        public static readonly string[] DicTypeStrs = { "Dictionary`2", "IDictionary`2" };

        static StackTrace _stackTrace = new StackTrace(true);

        /// <summary>
        /// 因为.net framework的环境不同
        /// </summary>
        /// <returns></returns>
        public static Type[] GetDefalt()
        {
            var frames = _stackTrace.GetFrames();

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

        private static object _syncRoot = new object();

        private static Dictionary<string, TypeInfo> _instanceCache = new Dictionary<string, TypeInfo>();
        /// <summary>
        /// 添加或获取实例
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static TypeInfo GetOrAddInstance(Type type, string methodName = "Add")
        {
            lock (_syncRoot)
            {

                if (type.IsInterface)
                {
                    throw new Exception("服务方法中不能包含接口内容！");
                }

                var fullName = type.FullName + methodName;

                if (!_instanceCache.TryGetValue(fullName, out TypeInfo typeInfo))
                {
                    var mi = type.GetMethod(methodName);

                    typeInfo = new TypeInfo()
                    {
                        Type = type,
                        Instance = Activator.CreateInstance(type),
                        FastInvokeHandler = mi == null ? null : FastInvoke.GetMethodInvoker(mi)
                    };
                    _instanceCache.TryAdd(fullName, typeInfo);
                }
                else
                {
                    typeInfo.Instance = Activator.CreateInstance(type);
                }
                return typeInfo;
            }
        }

        public static TypeInfo GetOrAddInstance(Type type, MethodInfo mb)
        {
            lock (_syncRoot)
            {
                if (type.IsInterface)
                {
                    throw new Exception("服务方法中不能包含接口内容！");
                }

                var fullName = type.FullName + mb.Name;

                if (!_instanceCache.TryGetValue(fullName, out TypeInfo typeInfo))
                {
                    typeInfo = new TypeInfo()
                    {
                        Type = type,
                        Instance = Activator.CreateInstance(type),
                        FastInvokeHandler = FastInvoke.GetMethodInvoker(mb)
                    };
                    _instanceCache.TryAdd(fullName, typeInfo);
                }
                else
                {
                    typeInfo.Instance = Activator.CreateInstance(type);
                }
                return typeInfo;
            }
        }




    }

    public class TypeInfo
    {
        public Type Type { get; set; }

        public Object Instance { get; set; }

        public FastInvoke.FastInvokeHandler FastInvokeHandler { get; set; }
    }
}
