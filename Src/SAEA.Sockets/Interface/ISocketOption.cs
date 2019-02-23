/***************************************************************************** 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets.Interface
*文件名： ISocketOption
*版本号： v4.1.2.5
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
*版本号： v4.1.2.5
*描述：
*****************************************************************************/
using SAEA.Sockets.Model;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace SAEA.Sockets.Interface
{
    public interface ISocketOption
    {
        SocketType SocketType { get; set; }

        bool WithSsl
        {
            get; set;
        }

        X509Certificate2 X509Certificate2
        {
            get; set;
        }

        SslProtocols SslProtocol
        {
            get; set;
        }

        bool NoDelay
        {
            get; set;
        } 


        bool UseIocp
        {
            get; set;
        }


        IContext Context
        {
            get; set;
        }

        bool UseIPV6
        {
            get; set;
        }

        string IP
        {
            get; set;
        }

        int Port
        {
            get; set;
        }

        int BufferSize
        {
            get; set;
        }

        int Count
        {
            get; set;
        }

        int TimeOut
        {
            get; set;
        }
    }
}
