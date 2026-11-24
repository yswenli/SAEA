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
*命名空间：SAEA.Http.Model
*文件名： WebConfig
*版本号： v26.4.23.1
*唯一标识：6cfd5869-0258-48ac-947e-c0d3318bbfd2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/28 14:19:50
*描述：WebConfig接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/24 16:56:10
*修改人： yswenli
*版本号： v26.11.24.1
*描述：新增 RequestDrainTimeout，控制 413 后请求体有界排空的超时
*
*****************************************************************************/
using System;
using System.Collections.Generic;

using SAEA.Common.NameValue;
using SAEA.Sockets.Pipeline;
using SAEA.Sockets.Ssl;

namespace SAEA.Http.Model
{
    /// <summary>
    /// Web 服务器配置，包含端口、根目录、缓存与超时等选项
    /// </summary>
    public class WebConfig
    {
        /// <summary>
        /// 监听端口
        /// </summary>
        internal int Port { get; set; }
        /// <summary>
        /// 根目录
        /// </summary>
        internal string Root { get; set; } = "wwwroot";

        /// <summary>
        /// 是否启用静态缓存
        /// </summary>
        internal bool IsStaticsCached { get; set; }

        /// <summary>
        /// 是压启用内容压缩
        /// </summary>
        internal bool IsZiped { get; set; }

        /// <summary>
        /// http处理数据缓存大小
        /// </summary>
        internal int HandleBufferSize { get; set; } = 64 * 1024;

        /// <summary>
        /// 初始化连接对象复用数
        /// </summary>
        internal int MaxConnects { get; set; } = 1000;

        /// <summary>
        /// 禁止访问列表
        /// </summary>
        public List<string> ForbiddenAccessList { get; set; } = new List<string>();

        /// <summary>
        /// 设置禁止访问列表
        /// </summary>
        /// <param name="list"></param>
        public void SetForbiddenAccessList(params string[] list)
        {
            if (list != null)
            {
                foreach (var item in list)
                {
                    if (!string.IsNullOrEmpty(item) && !ForbiddenAccessList.Contains(item))
                    {
                        ForbiddenAccessList.Add(item);
                    }
                }
            }
        }

        /// <summary>
        /// 默认controller/action
        /// </summary>
        public NameValueItem DefaultRoute
        {
            get; set;
        } = new NameValueItem() { Name = "home", Value = "index" };

        /// <summary>
        /// 默认的首页地址
        /// </summary>
        public string HomePage
        {
            get; set;
        } = "index.html";

        /// <summary>
        /// 支持跨域的header参数
        /// </summary>
        public static List<string> CrossHeaders
        {
            get; set;
        } = new List<string>();

        /// <summary>
        /// 是否是测试模式
        /// </summary>
        public bool IsDebug
        {
            get; set;
        } = false;

        /// <summary>
        /// 连接空闲回收时间，单位秒，默认 180，用于会话空闲判定与释放；action 执行超时见 <see cref="ActionTimeout"/>
        /// </summary>
        public double Timeout
        {
            get; set;
        } = 180;

        /// <summary>
        /// action 执行超时时间，单位秒，默认 0 表示不启用超时包装：同步 action 在当前线程直接执行，
        /// 异步 action 同步等待完成，阻塞或耗时过长都不会返回 408；大于 0 时才启用超时包装，超时返回 408 并取消令牌。
        /// 与连接空闲回收分开配置后，调小动作超时不会再提前回收连接
        /// </summary>
        public double ActionTimeout { get; set; } = 0;

        /// <summary>
        /// 请求体字节上限，默认 20MB；只限制报文体，不含请求行与报头。
        /// 必须在 WebHost.Start 之前设置，启动后再修改不会作用于已创建的连接。
        /// 取值必须大于 0；超限时在读完报头、请求体到齐之前就返回 413 并关闭连接。
        /// </summary>
        public int MaxRequestBodySize { get; set; } = 20 * 1024 * 1024;

        /// <summary>
        /// 413 之后请求体排空的超时时间，单位毫秒，默认 2000；取值 0 或负数表示不排空，直接关闭连接。
        /// 必须在 WebHost.Start 之前设置。排空期间服务端已半关发送方向，只继续读取剩余请求体，
        /// 读完或超时后关闭连接，避免带着未读字节关闭触发 RST。
        /// </summary>
        public int RequestDrainTimeout { get; set; } = 2000;

        /// <summary>
        /// 请求体落盘阈值，单位字节，默认 1MB；取值 0 表示禁用落盘，请求体始终留在内存。
        /// 必须在 WebHost.Start 之前设置；超过阈值的请求体会写入 <see cref="TempDirectory"/> 下的临时文件，
        /// 请求结束后删除。取值不能小于 0。
        /// </summary>
        public int LargeBodyThreshold { get; set; } = 1024 * 1024;

        /// <summary>
        /// 请求体临时文件目录，默认 null 表示系统临时目录下的 saea-http；启动期确保目录存在。
        /// 临时目录不可写时服务启动失败。
        /// </summary>
        public string TempDirectory { get; set; }

        /// <summary>
        /// 连接建立超时时间，单位秒，默认 3
        /// </summary>
        public double ConnectTimeout
        {
            get; set;
        } = 3;

        private readonly object _configurePipelineLock = new object();

        private Action<string, IChannelPipeline> _configurePipeline;

        private bool _configurePipelineFrozen;

        /// <summary>
        /// 管线装配回调：每条连接在处理器链装配阶段调用一次，接收连接标识与尚未启动的链。
        /// 回调里只能追加处理器，链启动后结构冻结；追加的入站处理器排在协议处理器之后、链尾异常处理器之前。
        /// 必须在 WebHost.Start 之前赋值，启动之后再赋值会抛 InvalidOperationException。
        /// </summary>
        public Action<string, IChannelPipeline> ConfigurePipeline
        {
            get
            {
                lock (_configurePipelineLock)
                {
                    return _configurePipeline;
                }
            }
            set
            {
                lock (_configurePipelineLock)
                {
                    if (_configurePipelineFrozen)
                        throw new InvalidOperationException("管线装配回调只能在 WebHost.Start 之前赋值，服务启动后已冻结。");

                    _configurePipeline = value;
                }
            }
        }

        /// <summary>
        /// 冻结管线装配回调，由 WebHost.Start 调用
        /// </summary>
        internal void FreezeConfigurePipeline()
        {
            lock (_configurePipelineLock)
            {
                _configurePipelineFrozen = true;
            }
        }

        private readonly object _sslLock = new object();

        private Action<SaeaTlsOptions> _configureSsl;

        private bool _sslFrozen;

        /// <summary>
        /// 设置 TLS 配置：必须写在 WebHost.Start 之前，服务启动后再调用会抛 InvalidOperationException。
        /// 配置后同一端口同时承载 HTTPS 与 WSS，WebSocket 升级与普通请求共用该加密连接。
        /// 重复调用以最后一次为准，未配置时保持明文 HTTP。
        /// </summary>
        /// <param name="configure">TLS 选项配置回调，不能为 null</param>
        public void UseSsl(Action<SaeaTlsOptions> configure)
        {
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            lock (_sslLock)
            {
                if (_sslFrozen)
                    throw new InvalidOperationException("TLS 配置只能在 WebHost.Start 之前设置，服务启动后已冻结。");

                _configureSsl = configure;
            }
        }

        /// <summary>
        /// 当前 TLS 配置回调，未配置时为 null
        /// </summary>
        internal Action<SaeaTlsOptions> ConfigureSsl
        {
            get
            {
                lock (_sslLock)
                {
                    return _configureSsl;
                }
            }
        }

        /// <summary>
        /// 冻结 TLS 配置，由 WebHost.Start 调用
        /// </summary>
        internal void FreezeSsl()
        {
            lock (_sslLock)
            {
                _sslFrozen = true;
            }
        }

        /// <summary>
        /// 业务请求处理入口：非空时取代默认的路由处理，由业务自行决定如何响应。
        /// </summary>
        public RequestDelegate RequestDelegate
        {
            get; set;
        }

        /// <summary>
        /// 业务异常处理入口：返回 null 表示继续按默认规则生成错误响应。
        /// </summary>
        public ExceptionHandler ExceptionHandler
        {
            get; set;
        }

        /// <summary>
        /// 连接关闭通知（连接标识, 异常）；正常断开时异常为 null。
        /// </summary>
        public Action<string, Exception> ConnectionClosed
        {
            get; set;
        }

    }
}