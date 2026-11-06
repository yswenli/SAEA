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
*命名空间：SAEA.Sockets.Core
*文件名： ChannelManager
*版本号： v26.4.23.1
*唯一标识：222b619f-8c2b-4151-add3-eb93e2f9da7d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/02/11 17:03:06
*描述：ChannelManager管理类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:27
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Sockets.Model;
using SAEA.Sockets.Ssl;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Sockets;

namespace SAEA.Sockets.Core
{
    /// <summary>
    /// 通道管理器：按通道标识保存套接字、流与 TLS 上下文，并记录空闲过期时间
    /// </summary>
    public class ChannelManager
    {
        ConcurrentDictionary<string, ChannelInfo> _concurrentDictionary = new ConcurrentDictionary<string, ChannelInfo>();

        int _timeOut = 60;

        /// <summary>
        /// 创建通道管理器。每个服务器实例应持有自己的实例。
        /// </summary>
        /// <param name="timeOut">通道空闲超时（秒）</param>
        public ChannelManager(int timeOut = 60)
        {
            _timeOut = timeOut;
        }

        /// <summary>
        /// 添加或更新通道，过期时间按构造时的超时从现在起算
        /// </summary>
        /// <param name="id">通道标识，空标识直接返回 null</param>
        /// <param name="socket">通道对应的套接字</param>
        /// <param name="stream">通道使用的流，可为 null</param>
        /// <param name="tls">TLS 上下文，未启用 TLS 时为 null</param>
        /// <returns>新建的通道信息，id 为空时为 null</returns>
        public ChannelInfo Set(string id, Socket socket, Stream stream, ISaeaTlsContext tls = null)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var ci = new ChannelInfo()
            {
                ID = id,
                ClientSocket = socket,
                Stream = stream,
                Tls = tls,
                Expired = DateTimeHelper.Now.AddSeconds(_timeOut)
            };
            _concurrentDictionary.AddOrUpdate(id, ci, (k, v) => ci);
            return ci;
        }


        /// <summary>
        /// 取通道并校验过期，已过期则先移除再返回 null
        /// </summary>
        /// <param name="id">通道标识</param>
        /// <returns>未过期的通道信息，不存在、已过期或 id 为空时为 null</returns>
        public ChannelInfo Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_concurrentDictionary.TryGetValue(id, out ChannelInfo ci))
            {
                if (ci != null)
                {
                    if (ci.Expired < DateTimeHelper.Now)
                        Remove(id);
                    else
                        return ci;
                }
            }
            return null;
        }


        /// <summary>
        /// 重置通道过期时间，通道不存在或 id 为空时不做处理
        /// </summary>
        /// <param name="id">通道标识</param>
        public void Refresh(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (_concurrentDictionary.TryGetValue(id, out ChannelInfo ci))
            {
                if (ci != null)

                    ci.Expired = DateTimeHelper.Now.AddSeconds(_timeOut);
            }
        }


        /// <summary>
        /// 移除通道记录，不关闭套接字
        /// </summary>
        /// <param name="id">通道标识</param>
        public void Remove(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            _concurrentDictionary.TryRemove(id, out ChannelInfo ci);
        }


        /// <summary>
        /// 关闭所有通道的套接字并清空通道记录
        /// </summary>
        public void Clear()
        {
            var vals = _concurrentDictionary.Values;

            if (vals != null && vals.Any())
            {
                foreach (var item in vals)
                {
                    try
                    {
                        item.ClientSocket.Shutdown(SocketShutdown.Both);
                    }
                    catch { }
                    item.ClientSocket.Close();
                }
            }
            _concurrentDictionary.Clear();
        }


        static ChannelManager _channelManager = null;

        /// <summary>
        /// 全局单例。仅为兼容旧代码保留；新代码应为每个服务器创建独立实例。
        /// </summary>
        [Obsolete("ChannelManager.Instance 为兼容旧代码保留。请为每个服务器创建独立的 ChannelManager 实例。")]
        public static ChannelManager Instance
        {
            get
            {
                if (_channelManager == null)
                {
                    _channelManager = new ChannelManager(int.MaxValue);
                }
                return _channelManager;
            }
        }

        /// <summary>
        /// 获取全局单例。仅为兼容旧代码保留。
        /// </summary>
        /// <param name="timeOut">首次创建单例时的通道空闲超时（秒），已创建则忽略</param>
        /// <returns>全局通道管理器</returns>
        [Obsolete("ChannelManager.GetInstance 为兼容旧代码保留。请为每个服务器创建独立的 ChannelManager 实例。")]
        public static ChannelManager GetInstance(int timeOut = 60)
        {
            if (_channelManager == null)
            {
                _channelManager = new ChannelManager(timeOut);
            }
            return _channelManager;
        }
    }
}