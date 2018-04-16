/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.WebAPI.Http
*文件名： HttpServer
*版本号： V1.0.0.0
*唯一标识：914acb72-d4c4-4fa1-8e80-ce2f83bd06f0
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/10 13:51:50
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/10 13:51:50
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.Sockets.Interface;
using SAEA.WebAPI.Common;
using SAEA.WebAPI.Http.Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SAEA.WebAPI.Http
{
    class HttpServer
    {
        ServerSocket _serverSocket;

        public HttpServer()
        {
            _serverSocket = new ServerSocket();
            _serverSocket.OnRequested += _serverSocket_OnRequested;
        }

        public void Start(int port = 39654)
        {
            _serverSocket.Start(port);
        }


        private void _serverSocket_OnRequested(IUserToken userToken, string htmlStr)
        {
            var httpContext = HttpContext.CreateInstance(this, userToken, htmlStr);

            var response = httpContext.Response;

            response.End();
        }

        internal void Replay(IUserToken userToken, byte[] data)
        {
            _serverSocket.Reply(userToken, data);
        }

        internal void Close(IUserToken userToken)
        {
            _serverSocket.Disconnected(userToken);
        }


    }
}
