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
*修改时间：2026/11/19 18:45:54
*修改人： yswenli
*版本号： v26.11.16.1
*描述：参数填充改为直接写入实参数组，令牌分支不再建参数列表与 ToArray 中间数组
*
*****************************************************************************/
using SAEA.Common.NameValue;
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
                // 直接写进目标数组：参数个数与顺序仍由 ParamsHelper 按声明顺序决定
                ParamsHelper.FillPamars(@params, nameValues, parameters);

                return parameters;
            }

            // 令牌不能交给参数填充：先剔除非令牌参数填充，再按原声明顺序把令牌插回原位
            var othersLength = 0;

            for (var i = 0; i < @params.Length; i++)
            {
                if (@params[i].ParameterType != typeof(CancellationToken))
                    othersLength++;
            }

            var others = new ParameterInfo[othersLength];

            var othersIndex = 0;

            for (var i = 0; i < @params.Length; i++)
            {
                if (@params[i].ParameterType != typeof(CancellationToken))
                    others[othersIndex++] = @params[i];
            }

            var filled = new object[othersLength];

            if (othersLength > 0)
                ParamsHelper.FillPamars(others, nameValues, filled);

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