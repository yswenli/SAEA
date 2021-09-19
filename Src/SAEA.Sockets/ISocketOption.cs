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
*命名空间：SAEA.Sockets.Interface
*文件名： ISocketOption
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
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
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
        /// 是否启用iocp
        /// </summary>
        bool UseIocp
        {
            get; set;
        }

        /// <summary>
        /// iocp模式下的编码会话上下文对象
        /// </summary>
        IContext Context
        {
            get; set;
        }

        /// <summary>
        /// ipv6
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
        /// port
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
        int Count
        {
            get; set;
        }

        /// <summary>
        /// 操作超时
        /// </summary>
        int TimeOut
        {
            get; set;
        }

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
