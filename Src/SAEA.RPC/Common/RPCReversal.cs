/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RPC.Common
*文件名： RPCInovker
*版本号： V2.2.1.1
*唯一标识：289c03b9-3910-4e15-8072-93243507689c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/17 14:11:30
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/17 14:11:30
*修改人： yswenli
*版本号： V2.2.1.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.RPC.Model;
using SAEA.RPC.Net;
using SAEA.RPC.Serialize;
using SAEA.Sockets.Interface;
using System;
using System.Linq;
using System.Reflection;

namespace SAEA.RPC.Common
{
    /// <summary>
    /// RPC将远程调用反转到本地服务
    /// </summary>
    internal class RPCReversal
    {
        static object _locker = new object();

        /// <summary>
        /// 执行方法
        /// </summary>
        /// <param name="method"></param>
        /// <param name="methodInvoker"></param>
        /// <param name="obj"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private static object ReversalMethod(MethodInfo method, FastInvoke.FastInvokeHandler methodInvoker, object obj, object[] args)
        {
            object result = null;
            try
            {
                var inputs = args;

                var @params = method.GetParameters();

                if (@params == null || @params.Length == 0)
                {
                    inputs = null;
                }
                result = methodInvoker.Invoke(obj, inputs);
            }
            catch (Exception ex)
            {
                throw new RPCPamarsException($"{obj}/{method.Name},用户自定义业务代码出现异常：{ex.Message}", ex);
            }
            return result;
        }


        public static object Reversal(IUserToken userToken, string serviceName, string methodName, object[] inputs)
        {
            var serviceInfo = RPCMapping.Get(serviceName, methodName);

            if (serviceInfo == null)
            {
                throw new RPCNotFundException($"当前请求找不到:{serviceName}/{methodName}", null);
            }

            var nargs = new object[] { userToken, serviceName, methodName, inputs };

            if (serviceInfo.FilterAtrrs != null && serviceInfo.FilterAtrrs.Count > 0)
            {
                foreach (var arr in serviceInfo.FilterAtrrs)
                {
                    var goOn = (bool)FastInvoke.GetMethodInvoker(arr.GetType().GetMethod("OnActionExecuting")).Invoke(arr, nargs.ToArray());

                    if (!goOn)
                    {
                        return null;
                    }
                }
            }

            if (serviceInfo.ActionFilterAtrrs != null && serviceInfo.ActionFilterAtrrs.Count > 0)
            {
                foreach (var arr in serviceInfo.ActionFilterAtrrs)
                {
                    var goOn = (bool)FastInvoke.GetMethodInvoker(arr.GetType().GetMethod("OnActionExecuting")).Invoke(arr, nargs.ToArray());

                    if (!goOn)
                    {
                        return null;
                    }
                }
            }

            var result = ReversalMethod(serviceInfo.Method, serviceInfo.MethodInvoker, serviceInfo.Instance, inputs);

            nargs = new object[] { userToken, serviceName, methodName, inputs, result };

            if (serviceInfo.FilterAtrrs != null && serviceInfo.FilterAtrrs.Count > 0)
            {
                foreach (var arr in serviceInfo.FilterAtrrs)
                {
                    FastInvoke.GetMethodInvoker(arr.GetType().GetMethod("OnActionExecuted")).Invoke(arr, nargs);
                }
            }

            if (serviceInfo.ActionFilterAtrrs != null && serviceInfo.ActionFilterAtrrs.Count > 0)
            {
                foreach (var arr in serviceInfo.FilterAtrrs)
                {
                    FastInvoke.GetMethodInvoker(arr.GetType().GetMethod("OnActionExecuted")).Invoke(arr, nargs);
                }
            }
            return result;
        }

        /// <summary>
        /// 反转到具体的方法上
        /// </summary>
        /// <param name="IUserToken"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static byte[] Reversal(IUserToken IUserToken, RSocketMsg msg)
        {
            byte[] result = null;

            object[] inputs = null;

            if (msg.Data != null)
            {
                var serviceInfo = RPCMapping.Get(msg.ServiceName, msg.MethodName);

                if (serviceInfo == null)

                    throw new RPCNotFundException($"找不到服务：{msg.ServiceName}/{msg.MethodName}");

                var ptypes = serviceInfo.Pamars.Values.ToArray();

                inputs = ParamsSerializeUtil.Deserialize(ptypes, msg.Data);
            }

            var r = Reversal(IUserToken, msg.ServiceName, msg.MethodName, inputs);

            if (r != null)
            {
                return ParamsSerializeUtil.Serialize(r);
            }
            return result;

        }
    }
}
