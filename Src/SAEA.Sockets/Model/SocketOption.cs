/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets.Model
*文件名： SocketOption
*版本号： v7.0.0.1
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
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/

using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using SAEA.Sockets.Interface;

namespace SAEA.Sockets.Model
{
    /// <summary>
    /// 套接字选项配置类
    /// </summary>
    public class SocketOption : ISocketOption
    {
        /// <summary>
        /// UDP最大长度
        /// </summary>
        public const int UDPMaxLength = 65506;

        /// <summary>
        /// 构造函数
        /// </summary>
        internal SocketOption() { }

        /// <summary>
        /// 套接字类型
        /// </summary>
        public SAEASocketType SocketType { get; set; } = SAEASocketType.Tcp;

        /// <summary>
        /// 是否使用SSL
        /// </summary>
        public bool WithSsl
        {
            get; set;
        } = false;

        /// <summary>
        /// SSL证书
        /// </summary>
        public X509Certificate2 X509Certificate2
        {
            get; set;
        }

        /// <summary>
        /// SSL协议版本
        /// </summary>
        public SslProtocols SslProtocol
        {
            get; set;
        }

        /// <summary>
        /// 是否禁用Nagle算法
        /// </summary>
        public bool NoDelay
        {
            get; set;
        } = true;

        /// <summary>
        /// 是否使用IOCP
        /// </summary>
        public bool UseIocp
        {
            get; set;
        }

        /// <summary>
        /// 编码会话上下文对象
        /// </summary>
        public IContext<ICoder> Context
        {
            get; set;
        }

        /// <summary>
        /// 是否使用IPv6
        /// </summary>
        public bool UseIPV6
        {
            get; set;
        } = false;

        /// <summary>
        /// IP地址
        /// </summary>
        public string IP
        {
            get; set;
        }

        /// <summary>
        /// 端口号
        /// </summary>
        public int Port
        {
            get; set;
        } = 39654;

        /// <summary>
        /// 读取缓冲区大小
        /// </summary>
        public int ReadBufferSize
        {
            get; set;
        } = 64 * 1024;

        /// <summary>
        /// 写入缓冲区大小
        /// </summary>
        public int WriteBufferSize
        {
            get; set;
        } = 64 * 1024;

        /// <summary>
        /// 最大连接数
        /// </summary>
        public int MaxConnects
        {
            get; set;
        } = 1000;

        /// <summary>
        /// 是否复用端口
        /// </summary>
        public bool ReusePort
        {
            get; set;
        } = true;

        /// <summary>
        /// 操作超时时间
        /// </summary>
        public int TimeOut
        {
            get; set;
        } = 60 * 1000;

        /// <summary>
        /// 空闲时间
        /// </summary>
        public int FreeTime
        {
            get; set;
        } = 180 * 1000;

        /// <summary>
        /// 组播地址
        /// </summary>
        public string MultiCastHost
        {
            set; get;
        }

        /// <summary>
        /// 是否广播
        /// </summary>
        public bool Broadcasted
        {
            get; set;
        }
    }
}
