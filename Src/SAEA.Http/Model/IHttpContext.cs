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
*命名空间：SAEA.Http.Model
*文件名： IHttpContext
*版本号： v26.4.23.1
*唯一标识：f7bd8435-41aa-4353-ad13-b1b894844d44
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/04/21 23:58:08
*描述：IHttpContext接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:29
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;

using SAEA.Http.Base;
using SAEA.Sockets.Interface;

namespace SAEA.Http.Model
{
    /// <summary>
    /// Http 上下文定义，提供请求、响应、会话与服务器工具入口
    /// </summary>
    public interface IHttpContext : IDisposable
    {
        /// <summary>
        /// 是否启用静态资源缓存
        /// </summary>
        bool IsStaticsCached { get; set; }
        /// <summary>
        /// 当前请求对象
        /// </summary>
        HttpRequest Request { get; }
        /// <summary>
        /// 当前响应对象
        /// </summary>
        HttpResponse Response { get; }
        /// <summary>
        /// Http 服务器实用工具
        /// </summary>
        HttpUtility Server { get; }
        /// <summary>
        /// 当前会话对象
        /// </summary>
        HttpSession Session { get; }
        /// <summary>
        /// Web 服务器配置
        /// </summary>
        WebConfig WebConfig { get; set; }

        /// <summary>
        /// WebSocket 能力入口
        /// </summary>
        IWebSocketsFeature WebSockets { get; }

        /// <summary>
        /// 自定义异常事件
        /// </summary>
        event ExceptionHandler OnException;

        /// <summary>
        /// 自定义http处理
        /// </summary>
        event RequestDelegate OnRequestDelegate;

        /// <summary>
        /// 解析请求并初始化请求、响应、会话对象
        /// </summary>
        /// <param name="userToken">客户端连接令牌</param>
        /// <param name="httpMessage">已解析的 Http 消息</param>
        void HttpHandle(IUserToken userToken, HttpMessage httpMessage);

        /// <summary>
        /// 获取业务处理结果
        /// </summary>
        /// <returns>业务处理结果，由响应负责输出</returns>
        IHttpResult GetActionResult();
    }
}