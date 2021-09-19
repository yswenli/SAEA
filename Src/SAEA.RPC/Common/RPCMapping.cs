/****************************************************************************
*Copyright (c) 2018-2021yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RPC.Provider
*文件名： ServiceTable
*版本号： v6.0.0.1
*唯一标识：e95f1d0b-f172-49c7-b75f-67f333504260
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/16 17:46:34
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/16 17:46:34
*修改人： yswenli
*版本号： v6.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.RPC.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace SAEA.RPC.Common
{
    /// <summary>
    /// 服务类缓存表
    /// md5+ServiceInfo反射结果
    /// </summary>
    internal static class RPCMapping
    {
        static object _locker = new object();

        static HashMap<string, string, ServiceInfo> _serviceMap = new HashMap<string, string, ServiceInfo>();

        /// <summary>
        /// 本地注册RPC服务缓存
        /// </summary>
        public static HashMap<string, string, ServiceInfo> ServiceMap
        {
            get
            {
                return _serviceMap;
            }
        }

        /// <summary>
        /// 本地注册RPC服务
        /// </summary>
        /// <param name="type"></param>
        public static void Regist(Type type)
        {
            lock (_locker)
            {
                var serviceName = type.Name;

                if (IsRPCService(type))
                {
                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

                    var rms = GetRPCMehod(methods);

                    if (rms.Count > 0)
                    {
                        foreach (var m in rms)
                        {
                            var tInfo = TypeHelper.GetOrAddInstance(type, m);

                            var serviceInfo = new ServiceInfo()
                            {
                                Type = type,
                                Instance = tInfo.Instance,
                                Method = m,
                                Pamars = m.GetParameters().ToDic()
                            };

                            //类上面的过滤
                            var attrs = type.GetCustomAttributes(true);

                            if (attrs != null && attrs.Length > 0)
                            {
                                var classAttrs = attrs.Where(b => b is ActionFilterAtrribute).ToList();

                                if (classAttrs != null && classAttrs.Any())
                                {

                                    serviceInfo.FilterAtrrs = new List<ActionFilterAtrribute>();

                                    foreach (var item in classAttrs)
                                    {
                                        serviceInfo.FilterAtrrs.Add((ActionFilterAtrribute)item);
                                    }

                                    serviceInfo.FilterAtrrs = serviceInfo.FilterAtrrs.OrderBy(b => b.Order).ToList();
                                }
                            }

                            //action上面的过滤
                            var actionAttrs = m.GetCustomAttributes(true);

                            if (actionAttrs != null)
                            {
                                var filterAttrs = actionAttrs.Where(b => b is ActionFilterAtrribute).ToList();

                                if (filterAttrs != null && filterAttrs.Any())
                                {
                                    serviceInfo.ActionFilterAtrrs = new List<ActionFilterAtrribute>();
                                    foreach (var item in filterAttrs)
                                    {
                                        serviceInfo.ActionFilterAtrrs.Add((ActionFilterAtrribute)item);
                                    };
                                    serviceInfo.ActionFilterAtrrs = serviceInfo.ActionFilterAtrrs.OrderBy(b => b.Order).ToList();
                                }
                            }
                            _serviceMap.Set(serviceName, m.Name, serviceInfo);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 本地注册RPC服务
        /// 若为空，则默认全部注册带有ServiceAttribute的服务
        /// </summary>
        /// <param name="types"></param>
        public static void Regists(params Type[] types)
        {
            if (types == null || !types.Any()) RegistAll();

            else

                foreach (var type in types)
                {
                    Regist(type);
                }
        }

        /// <summary>
        /// 全部注册带有ServiceAttribute的服务
        /// </summary>
        public static void RegistAll()
        {
            StackTrace ss = new StackTrace(true);

            var frames = ss.GetFrames();

            if (frames == null || !frames.Any()) throw new Exception("RPCMapping.RegistAll 获取上层调用异常！");

            foreach (var item in frames)
            {
                Regists(item.GetMethod().DeclaringType.Assembly.GetTypes());
            }
        }

        /// <summary>
        /// 判断类是否是RPCService
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsRPCService(Type type)
        {
            var isService = false;
            var cAttrs = type.GetCustomAttributes(true);
            if (cAttrs != null)
            {
                foreach (var cAttr in cAttrs)
                {
                    if (cAttr is RPCServiceAttribute)
                    {
                        isService = true;
                        break;
                    }
                }
            }
            return isService;
        }

        /// <summary>
        /// 获取RPC方法集合
        /// </summary>
        /// <param name="mInfos"></param>
        /// <returns></returns>
        public static List<MethodInfo> GetRPCMehod(MethodInfo[] mInfos)
        {
            List<MethodInfo> result = new List<MethodInfo>();
            if (mInfos != null)
            {
                var isRPC = false;
                foreach (var method in mInfos)
                {
                    if (method.IsAbstract || method.IsConstructor || method.IsFamily || method.IsPrivate || method.IsStatic)
                    {
                        break;
                    }

                    isRPC = true;
                    var attrs = method.GetCustomAttributes(true);
                    if (attrs != null)
                    {
                        foreach (var attr in attrs)
                        {
                            if (attr is NoRpcAttribute)
                            {
                                isRPC = false;
                                break;
                            }
                        }
                    }
                    if (isRPC)
                    {
                        result.Add(method);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 转换成字典
        /// </summary>
        /// <param name="parameterInfos"></param>
        /// <returns></returns>
        public static Dictionary<string, Type> ToDic(this ParameterInfo[] parameterInfos)
        {
            if (parameterInfos == null) return null;

            Dictionary<string, Type> dic = new Dictionary<string, Type>();

            foreach (var p in parameterInfos)
            {
                dic.Add(p.Name, p.ParameterType);
            }

            return dic;
        }


        /// <summary>
        /// 获取缓存内容
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="methodName"></param>
        /// <returns></returns>
        public static ServiceInfo Get(string serviceName, string methodName)
        {
            lock (_locker)
            {
                return _serviceMap.Get(serviceName, methodName);
            }
        }

        /// <summary>
        /// 获取缓存内容
        /// </summary>
        /// <returns></returns>
        public static List<string> GetServiceNames()
        {
            lock (_locker)
            {
                return _serviceMap.GetHashIDs();
            }
        }
        /// <summary>
        /// 获取服务的全部信息
        /// </summary>
        /// <param name="serviceName"></param>
        /// <returns></returns>
        public static Dictionary<string, ServiceInfo> GetAll(string serviceName)
        {
            lock (_locker)
            {
                return _serviceMap.GetAll(serviceName);
            }
        }



    }
}
