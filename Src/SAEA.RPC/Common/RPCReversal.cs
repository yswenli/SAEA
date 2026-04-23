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
*命名空间：SAEA.RPC.Common
*文件名： RPCReversal
*版本号： v26.4.23.1
*唯一标识：18399902-5d47-44e6-9fdb-b79d829f6a3a
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/05/25 17:28:26
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/05/25 17:28:26
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.Serialization;
using SAEA.RPC.Model;
using SAEA.RPC.Net;
using SAEA.Sockets.Interface;
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
        /// <param name="obj"></param>
        /// <param name="method"></param>        
        /// <param name="args"></param>
        /// <returns></returns>
        private static object ReversalMethod(object obj, MethodInfo method, object[] args)
        {
            var inputs = args;

            var @params = method.GetParameters();

            if (@params == null || @params.Length == 0)
            {
                inputs = null;
            }
            var result = method.Invoke(obj, inputs);

            return result;
        }


        public static object Reversal(IUserToken userToken, string serviceName, string methodName, object[] inputs)
        {
            var serviceInfo = RPCMapping.Get(serviceName, methodName);

            if (serviceInfo == null)
            {
                throw new RPCNotFundException($"当前请求找不到:{serviceName}/{methodName}", null);
            }

            #region 执行前

            var nargs = new object[] { userToken, serviceName, methodName, inputs };

            if (serviceInfo.FilterAtrrs != null && serviceInfo.FilterAtrrs.Any())
            {
                foreach (var arr in serviceInfo.FilterAtrrs)
                {
                    var goOn = (bool)arr.GetType().GetMethod(ConstHelper.ONACTIONEXECUTING).Invoke(arr, nargs.ToArray());

                    if (!goOn)
                    {
                        return null;
                    }
                }
            }

            if (serviceInfo.ActionFilterAtrrs != null && serviceInfo.ActionFilterAtrrs.Any())
            {
                foreach (var arr in serviceInfo.ActionFilterAtrrs)
                {
                    var goOn = (bool)arr.GetType().GetMethod(ConstHelper.ONACTIONEXECUTING).Invoke(arr, nargs.ToArray());

                    if (!goOn)
                    {
                        return null;
                    }
                }
            }

            #endregion

            var result = ReversalMethod(serviceInfo.Instance, serviceInfo.Method, inputs);

            #region 执行后

            nargs = new object[] { userToken, serviceName, methodName, inputs, result };

            if (serviceInfo.FilterAtrrs != null && serviceInfo.FilterAtrrs.Any())
            {
                foreach (var arr in serviceInfo.FilterAtrrs)
                {
                    arr.GetType().GetMethod(ConstHelper.ONACTIONEXECUTED).Invoke(arr, nargs);
                }
            }

            if (serviceInfo.ActionFilterAtrrs != null && serviceInfo.ActionFilterAtrrs.Any())
            {
                foreach (var arr in serviceInfo.ActionFilterAtrrs)
                {
                    arr.GetType().GetMethod(ConstHelper.ONACTIONEXECUTED).Invoke(arr, nargs);
                }
            }

            #endregion

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

                inputs = SAEASerialize.Deserialize(ptypes, msg.Data);
            }

            var r = Reversal(IUserToken, msg.ServiceName, msg.MethodName, inputs);

            if (r != null)
            {
                return SAEASerialize.Serialize(r);
            }
            return result;

        }
    }
}