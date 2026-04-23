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
*唯一标识：6ced41ec-8266-4a8a-a1fa-50f57756e9b8
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/04/21 23:58:08
*描述：
*
*=====================================================================
*修改标记
*修改时间：2019/04/21 23:58:08
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;

using SAEA.Http.Base;
using SAEA.Sockets.Interface;

namespace SAEA.Http.Model
{
    public interface IHttpContext : IDisposable
    {
        bool IsStaticsCached { get; set; }
        HttpRequest Request { get; }
        HttpResponse Response { get; }
        HttpUtility Server { get; }
        HttpSession Session { get; }
        WebConfig WebConfig { get; set; }

        /// <summary>
        /// 自定义异常事件
        /// </summary>
        event ExceptionHandler OnException;

        /// <summary>
        /// 自定义http处理
        /// </summary>
        event RequestDelegate OnRequestDelegate;

        void HttpHandle(IUserToken userToken, HttpMessage httpMessage);

        IHttpResult GetActionResult();
    }
}