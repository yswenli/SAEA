/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets.Model
*文件名： SocketOption
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
*
*****************************************************************************/

using SAEA.Sockets.Interface;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace SAEA.Sockets.Model
{
    public class SocketOption : ISocketOption
    {
        public const int UDPMaxLength = 65506;
        internal SocketOption() { }

        public SAEASocketType SocketType { get; set; } = SAEASocketType.Tcp;

        public bool WithSsl
        {
            get; set;
        } = false;

        public X509Certificate2 X509Certificate2
        {
            get; set;
        }

        public SslProtocols SslProtocol
        {
            get; set;
        }

        public bool NoDelay
        {
            get; set;
        } = true;


        public bool UseIocp
        {
            get; set;
        }


        public IContext Context
        {
            get; set;
        }

        public bool UseIPV6
        {
            get; set;
        } = false;

        public string IP
        {
            get; set;
        }

        public int Port
        {
            get; set;
        } = 39654;

        public int ReadBufferSize
        {
            get; set;
        } = 1024;

        public int WriteBufferSize
        {
            get; set;
        } = 1024;

        public int Count
        {
            get; set;
        } = 100;

        public bool ReusePort
        {
            get; set;
        } = true;

        public int TimeOut
        {
            get; set;
        } = 60 * 1000;
        public string MultiCastHost
        {
            set; get;
        }
        public bool Broadcasted
        {
            get; set;
        }
    }
}
