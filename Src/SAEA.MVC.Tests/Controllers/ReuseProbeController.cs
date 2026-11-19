/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.MVC.Tests.Controllers
*文件名： ReuseProbeController
*版本号： net10.0
*唯一标识：66c6be1e-3a48-4b37-8b8d-c4b31c7ea0c7
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 20:15:09
*描述：上下文跨请求复用的探针控制器：回显请求状态、制造响应副作用并提供 304 与输出缓存入口
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 20:15:09
*修改人： yswenli
*版本号： net10.0
*描述：新增，供同一条连接上连续请求的上下文复用用例取材
*
*****************************************************************************/
using SAEA.Http;
using SAEA.MVC.Attributes;
using SAEA.MVC.Controllers;
using SAEA.MVC.Hosting;
using SAEA.MVC.Results;

using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace SAEA.MVC.Tests.Controllers
{
    /// <summary>
    /// 上下文复用探针：把本次请求读到的状态拼成一行回显，并提供带响应副作用的入口。
    /// </summary>
    public class ReuseProbeController : Controller
    {
        private const string ProbeHeaderName = "X-Reuse-Probe";

        private static int _cachedExecutions;

        /// <summary>
        /// GET 入口，回显本次请求状态。
        /// </summary>
        /// <returns>分号分隔的键值串</returns>
        [HttpGetAttribute("reuse/probe")]
        public string ProbeGet()
        {
            return BuildEcho();
        }

        /// <summary>
        /// POST 入口，与 GET 共用同一路径，回显本次请求状态。
        /// </summary>
        /// <returns>分号分隔的键值串</returns>
        [HttpPostAttribute("reuse/probe")]
        public string ProbePost()
        {
            return BuildEcho();
        }

        /// <summary>
        /// 制造响应副作用：状态码走结果对象、写响应头、写响应 Cookie，供下一条请求检查是否残留。
        /// </summary>
        /// <returns>202 的文本结果</returns>
        [HttpGetAttribute("reuse/mark")]
        public ContentResult Mark()
        {
            var response = HttpContext.Current.Response;

            response.Headers["X-Reuse-Mark"] = "leak";

            response.Cookies["reuse-probe"] = new HttpCookie("reuse-probe", "leak");

            // 状态码必须由结果对象给出：响应收尾时 SetCached 会用结果的状态码覆盖 Response.Status
            return new ContentResult("reuse-mark", HttpStatusCode.Accepted);
        }

        /// <summary>
        /// 让本次响应走 304 分支：缓存判定参数置为已缓存且有有效期，SetCached 会改状态并返回缓存头。
        /// </summary>
        /// <returns>正常情况下不会写到客户端，304 不带正文</returns>
        [HttpGetAttribute("reuse/not-modified")]
        public string NotModified()
        {
            HttpContext.Current.Session.CacheCalcString = "1,60";

            return "reuse-not-modified-body";
        }

        /// <summary>
        /// 带输出缓存的入口，用于验证命中缓存后上下文仍能干净地交给下一条请求。
        /// </summary>
        /// <returns>带真实执行次数的文本</returns>
        [HttpGetAttribute("reuse/cached")]
        [OutputCache(60)]
        public string Cached()
        {
            var executions = Interlocked.Increment(ref _cachedExecutions);

            return "reuse-cached:" + executions;
        }

        /// <summary>
        /// 把本次请求的状态拼成一行：正文与集合长度用长度表示，避免正文里的分隔符干扰解析。
        /// </summary>
        /// <returns>分号分隔的键值串</returns>
        private static string BuildEcho()
        {
            var context = HttpContext.Current;

            var request = context.Request;

            string probeHeader = null;

            request.Headers.TryGetValue(ProbeHeaderName, out probeHeader);

            var bodyLength = request.Body == null ? -1 : request.Body.Length;

            var jsonLength = string.IsNullOrEmpty(request.Json) ? 0 : request.Json.Length;

            var fileCount = request.PostFiles == null ? -1 : request.PostFiles.Count;

            var builder = new StringBuilder(160);

            builder.Append("url=").Append(request.RelativeUrl)
                   .Append(";method=").Append(request.Method)
                   .Append(";jsonLen=").Append(jsonLength)
                   .Append(";bodyLen=").Append(bodyLength)
                   .Append(";probeHeader=").Append(probeHeader ?? "-")
                   .Append(";formKeys=").Append(JoinKeys(request.Forms))
                   .Append(";queryKeys=").Append(JoinKeys(request.Query))
                   .Append(";paramKeys=").Append(JoinKeys(request.Parmas))
                   .Append(";fileCount=").Append(fileCount)
                   .Append(";cookieCount=").Append(request.Cookies == null ? -1 : request.Cookies.Count)
                   .Append(";status=").Append((int)context.Response.Status)
                   .Append(";upgraded=").Append(context.Response.IsUpgraded)
                   .Append(";suppress=").Append(context.Response.SuppressBody)
                   .Append(";hash=").Append(RuntimeHelpers.GetHashCode(context));

            return builder.ToString();
        }

        /// <summary>
        /// 把集合的键拼成加号分隔串，空集合返回减号。
        /// </summary>
        /// <param name="keys">键集合，可为 null</param>
        /// <returns>键串或减号</returns>
        private static string JoinKeys(IDictionary<string, string> keys)
        {
            if (keys == null || keys.Count == 0)
                return "-";

            var builder = new StringBuilder();

            foreach (var key in keys.Keys)
            {
                if (builder.Length > 0)
                    builder.Append('+');

                builder.Append(key);
            }

            return builder.ToString();
        }
    }
}