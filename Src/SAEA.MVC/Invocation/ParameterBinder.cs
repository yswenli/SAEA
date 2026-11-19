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
*命名空间：SAEA.MVC.Invocation
*文件名： ParameterBinder
*版本号： v26.11.16.1
*唯一标识：71d958ac-0260-43f8-b302-c8b279119297
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 13:58:19
*描述：action 形参填充，CancellationToken 按声明位置注入
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 13:58:19
*修改人： yswenli
*版本号： v26.11.16.1
*描述：新增，承载 action 形参填充与 CancellationToken 的按声明位置注入
*
*****************************************************************************/
using SAEA.Common.NameValue;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace SAEA.MVC.Invocation
{
    /// <summary>
    /// action 形参填充：请求参数按声明顺序绑定，取消令牌由框架注入
    /// </summary>
    internal static class ParameterBinder
    {
        /// <summary>
        /// 按声明顺序填充 action 参数，框架注入 CancellationToken 类型的参数
        /// </summary>
        /// <param name="params">action 的参数声明</param>
        /// <param name="nameValues">请求参数</param>
        /// <param name="token">本次调用注入的取消令牌</param>
        /// <returns>与参数声明一一对应的实参数组</returns>
        internal static object[] FillParameters(ParameterInfo[] @params, NameValueCollection nameValues, CancellationToken token)
        {
            var parameters = new object[@params.Length];

            var hasToken = false;

            for (var i = 0; i < @params.Length; i++)
            {
                if (@params[i].ParameterType == typeof(CancellationToken))
                {
                    hasToken = true;
                    break;
                }
            }

            if (!hasToken)
            {
                var list = ParamsHelper.FillPamars(@params, nameValues);

                if (list == null) return parameters;

                return list.ToArray();
            }

            // 令牌不能交给参数填充：先剔除非令牌参数填充，再按原声明顺序把令牌插回原位
            var others = new List<ParameterInfo>(@params.Length);

            for (var i = 0; i < @params.Length; i++)
            {
                if (@params[i].ParameterType != typeof(CancellationToken))
                    others.Add(@params[i]);
            }

            List<object> filled = null;

            if (others.Count > 0)
                filled = ParamsHelper.FillPamars(others.ToArray(), nameValues);

            var filledIndex = 0;

            for (var i = 0; i < @params.Length; i++)
            {
                if (@params[i].ParameterType == typeof(CancellationToken))
                    parameters[i] = token;
                else
                    parameters[i] = filled[filledIndex++];
            }

            return parameters;
        }
    }
}