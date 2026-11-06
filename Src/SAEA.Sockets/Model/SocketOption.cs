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
*命名空间：SAEA.Sockets.Model
*文件名： SocketOption
*版本号： v26.4.23.1
*唯一标识：c7564e7e-6248-4e31-aec9-1f3052d9e871
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/01/23 11:16:53
*描述：SocketOption接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 13:50:40
*修改人： yswenli
*版本号： v26.11.5.1
*描述：输入处理器注释补充入站接管后的读取限制
*
*****************************************************************************/
using System;
using System.IO.Pipelines;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Sockets.Interface;
using SAEA.Sockets.Ssl;

namespace SAEA.Sockets.Model
{
    /// <summary>
    /// 套接字选项配置类
    /// </summary>
    public class SocketOption : ISocketOption, IPipelineSocketOption
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
        /// 是否暴露流门面（不切换引擎，TCP 始终 SAEA）。
        /// </summary>
        public bool UseStream
        {
            get; set;
        } = false;

        /// <summary>
        /// 统一 TLS 配置。
        /// </summary>
        public SaeaTlsOptions Tls
        {
            get { return _tls; }
            set { _tls = value ?? new SaeaTlsOptions(); }
        }

        SaeaTlsOptions _tls = new SaeaTlsOptions();

        /// <summary>
        /// 是否使用SSL
        /// </summary>
        public bool WithSsl
        {
            get { return _tls.Enabled; }
            set { _tls.Enabled = value; }
        }

        /// <summary>
        /// SSL证书
        /// </summary>
        public X509Certificate2 X509Certificate2
        {
            get { return _tls.ServerCertificate; }
            set { _tls.ServerCertificate = value; }
        }

        /// <summary>
        /// SSL协议版本
        /// </summary>
        public SslProtocols SslProtocol
        {
            get { return _tls.Protocols; }
            set { _tls.Protocols = value; }
        }

        /// <summary>
        /// 是否禁用Nagle算法
        /// </summary>
        public bool NoDelay
        {
            get; set;
        } = true;

        /// <summary>
        /// 是否使用包模式
        /// </summary>
        public bool UsePacket
        {
            get; set;
        } = true;

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
        } = false;

        /// <summary>
        /// 操作超时时间
        /// </summary>
        public int ActionTimeout
        {
            get; set;
        } = 180 * 1000;

        /// <summary>
        /// 空闲时间
        /// </summary>
        public int FreeTime
        {
            get; set;
        } = 180 * 1000;

        /// <summary>
        /// 连接超时时间
        /// </summary>
        public int ConnectTimeout
        {
            get; set;
        } = 30 * 1000;

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

        /// <summary>
        /// 连接建立回调，在通道登记完成后、通知 OnAccepted 之前同步调用；未配置时为 null。
        /// </summary>
        public Action<ChannelInfo> ConnectionOpened
        {
            get; set;
        }

        /// <summary>
        /// 输入处理器，接管该连接的读循环，独占该连接的 PipeReader；
        /// 接管后其他入站读取入口（GetStream / ReceiveAsync / ChannelInfo.Stream）会抛 InvalidOperationException。未配置时为 null。
        /// </summary>
        public Func<IUserToken, PipeReader, IReceiveFlowControl, CancellationToken, Task> InputHandler
        {
            get; set;
        }
    }
}