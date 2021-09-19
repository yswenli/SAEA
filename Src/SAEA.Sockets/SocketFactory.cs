/***************************************************************************** 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c) 2018-2021yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets
*文件名： SocketFactory
*版本号： v6.0.0.1
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： v6.0.0.1
*描述：
*****************************************************************************/
using SAEA.Sockets.Core.Tcp;
using SAEA.Sockets.Core.Udp;
using SAEA.Sockets.Model;
using System;
using System.Threading;

namespace SAEA.Sockets
{
    /// <summary>
    /// Socket工厂类
    /// </summary>
    public static class SocketFactory
    {

        /// <summary>
        /// 创建服务器Socket
        /// </summary>
        /// <param name="socketOption"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static IServerSocket CreateServerSocket(ISocketOption socketOption, CancellationToken cancellationToken)
        {
            if (socketOption.SocketType == SAEASocketType.Tcp)
            {
                if (!socketOption.UseIocp)
                {
                    return new StreamServerSocket(socketOption, cancellationToken);
                }
                else
                {
                    return new IocpServerSocket(socketOption);
                }
            }
            else
            {
                if (!socketOption.UseIocp)
                {
                    throw new NotImplementedException("udp不支持此模式");
                }
                else
                {
                    return new UdpServerSocket(socketOption);
                }
            }
        }

        /// <summary>
        /// 创建iocp服务器Socket
        /// </summary>
        /// <param name="socketOption"></param>
        /// <returns></returns>
        public static IServerSocket CreateServerSocket(ISocketOption socketOption)
        {
            if (socketOption.SocketType == SAEASocketType.Tcp)
            {
                if (socketOption.UseIocp)
                {
                    return new IocpServerSocket(socketOption);
                }
                else
                {
                    CancellationTokenSource source = new CancellationTokenSource();
                    CancellationToken token = source.Token;
                    return new StreamServerSocket(socketOption, token);
                }
            }
            else
            {
                if (socketOption.UseIocp)
                {
                    return new UdpServerSocket(socketOption);
                }
                else
                {
                    throw new NotImplementedException("udp不支持此模式");
                }
            }
        }


        /// <summary>
        /// 创建客户端Socket
        /// </summary>
        /// <param name="socketOption"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static IClientSocket CreateClientSocket(ISocketOption socketOption, CancellationToken cancellationToken)
        {
            if (socketOption.SocketType == SAEASocketType.Tcp)
            {
                if (!socketOption.UseIocp)
                {
                    return new StreamClientSocket(socketOption, cancellationToken);
                }
                else
                {
                    return new IocpClientSocket(socketOption);
                }
            }
            else
            {
                if (!socketOption.UseIocp)
                {
                    throw new NotImplementedException("udp不支持此模式");
                }
                else
                {
                    return new UdpClientSocket(socketOption);
                }
            }

        }

        /// <summary>
        /// 创建客户端Socket
        /// </summary>
        /// <param name="socketOption"></param>
        /// <returns></returns>
        public static IClientSocket CreateClientSocket(ISocketOption socketOption)
        {
            if (socketOption.SocketType == SAEASocketType.Tcp)
            {
                if (!socketOption.UseIocp)
                {
                    return new StreamClientSocket(socketOption);
                }
                else
                {
                    return new IocpClientSocket(socketOption);
                }
            }
            else
            {
                if (!socketOption.UseIocp)
                {
                    throw new NotImplementedException("udp不支持此模式");
                }
                else
                {
                    return new UdpClientSocket(socketOption);
                }
            }

        }

    }
}
