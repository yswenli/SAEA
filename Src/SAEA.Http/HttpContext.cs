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
*命名空间：SAEA.Http
*文件名： HttpContext
*版本号： v26.4.23.1
*唯一标识：c4a82e3a-656b-4b4e-9042-976bdd881fc1
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/28 14:19:50
*描述：HttpContext上下文类
*
*=====================================================================
*修改标记
*修改时间：2026/11/26 10:00:00
*修改人： yswenli
*版本号： v26.11.25.1
*描述：静态文件结果写出前按请求 Range 头应用单区间范围
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Http.Base;
using SAEA.Http.Common;
using SAEA.Http.Model;
using SAEA.Sockets.Pipeline;

using System;

namespace SAEA.Http
{
    /// <summary>
    /// SAEA.Http http上下文
    /// </summary>
    internal class HttpContext : HttpContextBase, IHttpContext, IReusableHttpContext
    {
        /// <summary>
        /// SAEA.Http http上下文
        /// </summary>
        /// <param name="webHost"></param>
        public HttpContext(IWebHost webHost) : base(webHost)
        {

        }

        /// <summary>
        /// 处理业务逻辑
        /// </summary>
        /// <param name="channel">连接通道</param>
        /// <param name="httpMessage">Http消息</param>
        public override void HttpHandle(IChannel channel, HttpMessage httpMessage)
        {
            base.HttpHandle(channel, httpMessage);

            IHttpResult result = null;

            try
            {

                // HEAD 复用 GET 的路由与参数合并，但只回报头：必须抢在业务执行前置位，
                // 静态文件与 SSE 等出口在写正文时都会按 SuppressBody 丢弃内容
                if (Request.Method == ConstHelper.HEAD)
                    Response.SuppressBody = true;

                switch (Request.Method)
                {
                    case ConstHelper.GET:
                    case ConstHelper.HEAD:
                    case ConstHelper.POST:
                    case ConstHelper.PUT:
                    case ConstHelper.DELETE:
                    case ConstHelper.PATCH:

                        if (Request.Query.Count > 0)
                        {
                            foreach (var item in Request.Query)
                            {
                                Request.Parmas[item.Key] = item.Value;
                            }
                        }
                        if (Request.Forms.Count > 0)
                        {
                            foreach (var item in Request.Forms)
                            {
                                Request.Parmas[item.Key] = item.Value;
                            }
                        }
                        if (WebConfig != null && WebConfig.RequestDelegate != null)
                        {
                            WebConfig.RequestDelegate(this);
                        }
                        else
                        {
                            result = GetActionResult();
                        }
                        break;
                    case ConstHelper.OPTIONS:
                        result = new HttpEmptyResult();
                        break;
                    default:
                        // 505 是「HTTP 版本不支持」，与「请求方法不认识」语义不同，不能拿它作答；
                        // 方法不在分发范围内的标准应答是 501
                        result = new HttpContentResult("不支持的请求方式", System.Net.HttpStatusCode.NotImplemented);
                        break;
                }
            }
            catch (Exception ex)
            {
                result = WebConfig == null || WebConfig.ExceptionHandler == null
                    ? null
                    : WebConfig.ExceptionHandler(this, ex);
            }

            // 确保连接被释放
            if (result == null)
            {
                // 没有结果说明业务已自行写出响应（或没有可写内容），按短连接收尾：
                // End 会先强制 Connection: close 再关闭，重复调用在 Channel 置空后自然失效。
                Response.End();
            }
            else if (!(result is IBigDataResult))
            {
                // 非大数据结果，设置缓存后按连接策略收尾
                if (result is IFileResult && _webHost.WebConfig.IsStaticsCached)
                {
                    Response.SetCached(result, "60,60");
                }
                else
                {
                    Response.SetCached(result, this.Session.CacheCalcString);
                }

                // 文件正文已在 SetCached 中装好，此时才能按完整长度计算单区间并切片
                if (result is IFileResult)
                {
                    Request.Headers.TryGetValue("Range", out var rangeHeader);

                    Response.ApplyRangeHeader(rangeHeader);
                }

                // 连接复用由请求协议与请求的 Connection 头决定：HTTP/1.1 默认保持，
                // HTTP/1.0 默认关闭；响应头与 Send/End 的连接副作用必须一致。
                var connection = Request.Headers.ContainsKey("Connection") ? Request.Headers["Connection"] : null;

                if (Response.ApplyConnectionPolicy(Request.Protocal, connection))
                {
                    Response.Send();
                }
                else
                {
                    Response.End();
                }
            }
            // IBigDataResult类型会在其他地方处理连接释放
        }
        public override IHttpResult GetActionResult()
        {
            string url = Request.Url;

            //禁止访问
            var flist = WebConfig.ForbiddenAccessList;

            if (flist.Count > 0)
            {
                foreach (var item in flist)
                {
                    if (url.IndexOf(item.ToUpper(), StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return new HttpContentResult("o_o，当前内容禁止访问！", System.Net.HttpStatusCode.Forbidden);
                    }
                    if (System.Text.RegularExpressions.Regex.IsMatch(url, item))
                    {
                        return new HttpContentResult("o_o，当前内容禁止访问！", System.Net.HttpStatusCode.Forbidden);
                    }
                }
            }

            if (url == "/")
            {
                url = "/index.html";
            }

            var filePath = Server.MapPath(url);

            if (filePath == null)
            {
                return new HttpContentResult("o_o，找不到任何内容", System.Net.HttpStatusCode.NotFound);
            }

            if (StaticResourcesCache.Exists(filePath))
            {
                if (StaticResourcesCache.IsBigFile(filePath))
                {
                    return new HttpBigDataResult(filePath);
                }
                else
                {
                    return new HttpFileResult(filePath, IsStaticsCached);
                }
            }
            return new HttpContentResult("o_o，找不到任何内容", System.Net.HttpStatusCode.NotFound);
        }
    }
}