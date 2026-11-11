/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.P2P.TurnServer
*文件名： UdpTurnControlChannel
*版本号： v26.11.5.1
*唯一标识：9aaf7dfc-b77c-4b8d-bda8-8c6bed4ca0cd
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 01:30:00
*描述：基于 STUN 监听端点的 UDP 控制连接
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 01:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增基于 STUN 监听端点的 UDP 控制连接
*
*****************************************************************************/
using System;
using System.Net;
using System.Threading.Tasks;

using SAEA.P2P.Common;
using SAEA.P2P.StunServer;

namespace SAEA.P2P.TurnServer
{
    /// <summary>
    /// UDP 控制连接：把 STUN 监听端点与客户端来源端点组合成一条可回送的控制连接。
    /// 发送是异步投递，失败只记日志，不向上抛给中继接收循环。
    /// </summary>
    internal sealed class UdpTurnControlChannel : ITurnControlChannel
    {
        /// <summary>
        /// 接收请求的 STUN 监听端点
        /// </summary>
        private readonly StunServerEndpoint _endpoint;

        /// <summary>
        /// 包装一个监听端点与客户端端点
        /// </summary>
        /// <param name="endpoint">STUN 监听端点</param>
        /// <param name="remoteEndPoint">客户端来源端点</param>
        /// <exception cref="ArgumentNullException">任一参数为 null</exception>
        public UdpTurnControlChannel(StunServerEndpoint endpoint, IPEndPoint remoteEndPoint)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }
            if (remoteEndPoint == null)
            {
                throw new ArgumentNullException(nameof(remoteEndPoint));
            }

            _endpoint = endpoint;
            RemoteEndPoint = remoteEndPoint;
        }

        /// <summary>
        /// 客户端来源端点
        /// </summary>
        public IPEndPoint RemoteEndPoint { get; private set; }

        /// <summary>
        /// 监听端点；已释放时为 null
        /// </summary>
        public IPEndPoint LocalEndPoint
        {
            get { return _endpoint.LocalEndPoint; }
        }

        /// <summary>
        /// UDP 是数据报连接，ChannelData 不补齐
        /// </summary>
        public bool IsStream
        {
            get { return false; }
        }

        /// <summary>
        /// 通过监听端点把一个数据报发回客户端
        /// </summary>
        /// <param name="data">待发送的完整报文</param>
        public void Send(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return;
            }

            try
            {
                var task = _endpoint.SendAsync(RemoteEndPoint, data);
                if (task != null && !task.IsCompleted)
                {
                    task.ContinueWith(
                        t =>
                        {
                            var ignored = t.Exception;
                        },
                        TaskContinuationOptions.OnlyOnFaulted);
                }
            }
            catch (Exception ex)
            {
                P2PLogHelper.Debug("TurnServer", "回送客户端失败：" + ex.Message);
            }
        }
    }
}