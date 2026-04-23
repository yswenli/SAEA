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
*命名空间：SAEA.Http.Base.Net
*文件名： IHttpSocket
*版本号： v26.4.23.1
*唯一标识：7598942e-b6e8-426f-8b1c-9674665e3e88
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/07/31 10:13:22
*描述：IHttpSocket接口
*
*=====================================================================
*修改标记
*修改时间：2020/07/31 10:13:22
*修改人： yswenli
*版本号： v26.4.23.1
*描述：IHttpSocket接口
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