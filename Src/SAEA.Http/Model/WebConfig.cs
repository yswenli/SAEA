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
*修改时间：2026/11/11 11:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：装配回调改为启动前注入：Start/Connect 之后再赋值抛 InvalidOperationException，中英 README 同步说明
*
*****************************************************************************/
using System;
using System.Collections.Generic;

using SAEA.Common.NameValue;
using SAEA.Sockets.Pipeline;

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
        /// 连接空闲超时时间，单位秒，默认 180，同时用于读写超时与会话释放时间
        /// </summary>
        public double Timeout
        {
            get; set;
        } = 180;

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