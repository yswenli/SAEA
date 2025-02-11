﻿/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http.Model
*文件名： IWebHost
*版本号： v7.0.0.1
*唯一标识：340c3ef0-2e98-4f25-998f-2bb369fa2794
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/10/12 00:48:06
*描述：
*
*=====================================================================
*修改标记
*创建时间：2018/10/12 00:48:06
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/

using SAEA.Http.Base;
using SAEA.Sockets.Interface;

namespace SAEA.Http.Model
{

    public interface IWebHost
    {
        bool IsRunning { get; set; }

        WebConfig WebConfig { get; set; }

        HttpUtility HttpUtility { get; }

        object RouteParam { get; set; }

        /// <summary>
        /// 自定义异常事件
        /// </summary>
        event ExceptionHandler OnException;

        void Start();

        void Send(IUserToken userToken, byte[] data);

        void Disconnect(IUserToken userToken);

        void End(IUserToken userToken, byte[] data);

        void Stop();
    }
}