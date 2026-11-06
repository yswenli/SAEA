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
*命名空间：SAEA.Sockets
*文件名： ISocketOption
*版本号： v26.4.23.1
*唯一标识：d0343459-13ab-478e-acad-220d4a340d84
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/08/22 10:00:37
*描述：ISocketOption接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:27
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using SAEA.Sockets.Ssl;

using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace SAEA.Sockets
{
    /// <summary>
    /// 配置选项
    /// </summary>
    public interface ISocketOption
    {
        /// <summary>
        /// socket类型
        /// </summary>
        SAEASocketType SocketType { get; set; }

        /// <summary>
        /// 是否暴露流门面（不切换引擎，TCP 始终 saea）。
        /// </summary>
        bool UseStream
        {
            get; set;
        }

        /// <summary>
        /// 统一 TLS 配置。WithSsl 为兼容属性，内部映射到 Tls.Enabled。
        /// </summary>
        SaeaTlsOptions Tls
        {
            get; set;
        }

        /// <summary>
        /// 是否使用流模式的SSL
        /// </summary>
        bool WithSsl
        {
            get; set;
        }

        /// <summary>
        /// ssl证书
        /// </summary>
        X509Certificate2 X509Certificate2
        {
            get; set;
        }

        /// <summary>
        /// ssl版本
        /// </summary>
        SslProtocols SslProtocol
        {
            get; set;
        }

        /// <summary>
        /// 是否立即响应
        /// </summary>
        bool NoDelay
        {
            get; set;
        }


        /// <summary>
        /// 是否启用包模式
        /// </summary>
        bool UsePacket
        {
            get; set;
        }

        /// <summary>
        /// saea模式下的编码会话上下文对象
        /// </summary>
        IContext<ICoder> Context
        {
            get; set;
        }

        /// <summary>
        /// 是否使用 IPv6 套接字
        /// </summary>
        bool UseIPV6
        {
            get; set;
        }

        /// <summary>
        /// ip地址
        /// </summary>
        string IP
        {
            get; set;
        }

        /// <summary>
        /// 端口号
        /// </summary>
        int Port
        {
            get; set;
        }

        /// <summary>
        /// 读取缓冲区大小
        /// </summary>
        int ReadBufferSize
        {
            get; set;
        }

        /// <summary>
        /// 写入缓冲区大小
        /// </summary>
        int WriteBufferSize { get; set; }

        /// <summary>
        /// 服务器中支持的客户端数
        /// </summary>
        int MaxConnects
        {
            get; set;
        }

        /// <summary>
        /// 操作超时
        /// </summary>
        int ActionTimeout
        {
            get; set;
        }

        /// <summary>
        /// 连接超时
        /// </summary>
        int ConnectTimeout
        {
            get; set;
        }

        /// <summary>
        /// 空闲超时
        /// </summary>
        int FreeTime
        {
            get; set;
        }

        /// <summary>
        /// 是否端口复用
        /// </summary>
        bool ReusePort { get; set; }

        /// <summary>
        /// 组播
        /// </summary>
        string MultiCastHost { get; set; }

        /// <summary>
        /// 广播
        /// </summary>
        bool Broadcasted { get; set; }
    }
}
