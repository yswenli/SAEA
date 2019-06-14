/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.TcpP2P
*文件名： NATServer
*版本号： v4.5.6.7
*唯一标识：435d783f-c85f-4a2c-b655-bbcfff38c790
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 21:00:15
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 21:00:15
*修改人： yswenli
*版本号： v4.5.6.7
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Sockets.Interface;
using SAEA.TcpP2P.Net;
using System;

namespace SAEA.TcpP2P
{
    public class NATServer
    {
        Receiver _server;

        public NATServer()
        {
            _server = new Receiver();
            _server.OnAccepted += _server_OnAccepted;
            _server.OnDisconnected += _server_OnDisconnected;
        }

        private void _server_OnAccepted(object userToken)
        {
            var ut = userToken as IUserToken;
            ConsoleHelper.WriteLine("userToken.ID:{0} 连接成功！", ut.ID);
        }

        private void _server_OnDisconnected(string ID, Exception ex)
        {
            ConsoleHelper.WriteLine("userToken.ID:{0} 已断开连接！", ID);
        }

        public void Start()
        {
            _server.Start();
        }

    }
}
