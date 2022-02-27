/****************************************************************************
*Copyright (c) 2018-2022yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http.Base.Net
*文件名： HttpSocket
*版本号： v7.0.0.1
*唯一标识：ab912b9a-c7ed-44d9-8e48-eef0b6ff86a2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/8 17:11:15
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/8 17:11:15
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Sockets.Interface;
using System;

namespace SAEA.Http.Base.Net
{
    interface IHttpSocket
    {
        event Action<IUserToken, HttpMessage> OnRequested;

        event Action<Exception> OnError;

        void Disconnecte(IUserToken userToken);
        void End(IUserToken userToken, byte[] data);
        void Send(IUserToken userToken, byte[] data);
        void Start();
        void Stop();
    }
}