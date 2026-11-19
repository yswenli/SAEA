/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.MVC.Attributes
*文件名： HttpMethodCatalog
*版本号： v26.11.16.1
*唯一标识：1676ca71-f336-43f8-bfc9-7308872d1e9f
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 15:55:14
*描述：动词特性清单与 Allow 输出顺序
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 16:54:00
*修改人： yswenli
*版本号： v26.11.16.1
*描述：新增 ConflictOrder，供属性路由冲突判定使用，排除 HEAD 与 OPTIONS
*
*****************************************************************************/
using SAEA.Common;

using System;
using System.Collections.Generic;

namespace SAEA.MVC.Attributes
{
    /// <summary>
    /// 动词特性清单：统一全部动词、Allow 顺序与特性筛选口径
    /// </summary>
    internal static class HttpMethodCatalog
    {
        /// <summary>
        /// 支持的全部动词，同时作为声明动词的去重与排序基准；未声明动词时按该清单补写路由键
        /// </summary>
        internal static readonly string[] All = { ConstHelper.GET, ConstHelper.POST, ConstHelper.PUT, ConstHelper.DELETE, ConstHelper.PATCH, ConstHelper.HEAD, ConstHelper.OPTIONS };

        /// <summary>
        /// 冲突判定用的动词集合：未声明动词的条目实际占用的动词，不含 HEAD 与 OPTIONS。
        /// 这类条目并不接管 OPTIONS，HEAD 也优先交给显式 [HttpHead]、否则走 GET 兜底，
        /// 因此显式声明这两个动词的条目与它同路径是合法的
        /// </summary>
        internal static readonly string[] ConflictOrder = { ConstHelper.GET, ConstHelper.POST, ConstHelper.PUT, ConstHelper.DELETE, ConstHelper.PATCH };

        /// <summary>
        /// Allow 报头的基础输出顺序，共六项；OPTIONS 不在基础序列内，只有显式声明 [HttpOptions] 时
        /// 才由 WithExplicitOptions 追加到末位
        /// </summary>
        internal static readonly string[] AllowOrder = { ConstHelper.GET, ConstHelper.HEAD, ConstHelper.POST, ConstHelper.PUT, ConstHelper.DELETE, ConstHelper.PATCH };

        /// <summary>
        /// 判断方法名是否为 HEAD，比较忽略大小写
        /// </summary>
        /// <param name="method">请求方法名，可为 null</param>
        /// <returns>是 HEAD 时返回 true</returns>
        internal static bool IsHead(string method)
        {
            return string.Compare(method, ConstHelper.HEAD, true) == 0;
        }

        /// <summary>
        /// 判断方法名是否为 OPTIONS，比较忽略大小写
        /// </summary>
        /// <param name="method">请求方法名，可为 null</param>
        /// <returns>是 OPTIONS 时返回 true</returns>
        internal static bool IsOptions(string method)
        {
            return string.Compare(method, ConstHelper.OPTIONS, true) == 0;
        }

        /// <summary>
        /// 按显式声明补写 OPTIONS：两个 Allow 生成器共用，保证顺序与出现条件一致
        /// </summary>
        /// <param name="allow">已按 AllowOrder 生成的 Allow 文本，可为 null</param>
        /// <param name="declaresOptions">该 action 或路径是否显式声明了 OPTIONS</param>
        /// <returns>补写后的 Allow 文本；没有任何可列出的方法时返回 null</returns>
        internal static string WithExplicitOptions(string allow, bool declaresOptions)
        {
            // OPTIONS 排在末位且只在显式声明时出现：它是框架自动应答的方法，不能由不限谓词或 GET 隐含得到
            if (!declaresOptions) return allow;

            if (string.IsNullOrEmpty(allow)) return ConstHelper.OPTIONS;

            return allow + ", " + ConstHelper.OPTIONS;
        }

        /// <summary>
        /// 判断方法是否会修改资源，用于区分读写语义
        /// </summary>
        /// <param name="method">请求方法名，可为 null</param>
        /// <returns>POST/PUT/DELETE/PATCH 返回 true，其余返回 false</returns>
        internal static bool IsMutating(string method)
        {
            return string.Compare(method, ConstHelper.POST, true) == 0
                || string.Compare(method, ConstHelper.PUT, true) == 0
                || string.Compare(method, ConstHelper.DELETE, true) == 0
                || string.Compare(method, ConstHelper.PATCH, true) == 0;
        }

        /// <summary>
        /// 从特性集合里筛出动词特性，保持原有声明顺序
        /// </summary>
        /// <param name="attributes">方法上的全部特性，可为 null</param>
        /// <returns>动词特性数组；没有时返回空数组，不返回 null</returns>
        internal static HttpMethodAttribute[] FromAttributes(object[] attributes)
        {
            if (attributes == null || attributes.Length == 0) return new HttpMethodAttribute[0];

            var result = new List<HttpMethodAttribute>();

            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i] as HttpMethodAttribute;

                if (attribute != null) result.Add(attribute);
            }

            return result.ToArray();
        }
    }
}