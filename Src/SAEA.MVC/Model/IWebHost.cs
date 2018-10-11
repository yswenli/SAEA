/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.MVC.Model
*文件名： IWebHost
*版本号： V2.2.0.0
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
*版本号： V2.2.0.0
*描述：
*
*****************************************************************************/

using SAEA.MVC.Http.Base;
using SAEA.Sockets.Interface;
using System;

namespace SAEA.MVC.Model
{
    internal interface IWebHost
    {
        bool IsRunning { get; set; }
        WebConfig WebConfig { get; set; }

        event Action<IUserToken, RequestDataReader> OnRequested;        

        void Start();

        void Reponse(IUserToken userToken, byte[] data);

        void Stop();
    }
}