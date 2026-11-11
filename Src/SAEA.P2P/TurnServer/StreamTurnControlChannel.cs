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
*文件名： StreamTurnControlChannel
*版本号： v26.11.5.1
*唯一标识：92bca777-637a-49bf-b0c4-c7f20c49b1b6
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 02:30:00
*描述：基于字节流的 TLS 控制连接
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 03:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：构造新增 isTls，并暴露底层流作为数据流
*
*****************************************************************************/
using System;
using System.IO;
using System.Net;

using SAEA.P2P.Common;

namespace SAEA.P2P.TurnServer
{
    /// <summary>
    /// 流式控制连接：把一条已完成 TLS 握手的双向字节流包装成 TURN 控制连接。
    /// 同一连接上可能有中继接收循环与请求处理线程同时写，发送必须串行化，否则会切碎 ChannelData 帧。
    /// </summary>
    internal sealed class StreamTurnControlChannel : ITurnControlChannel
    {
        /// <summary>
        /// 已握手的双向字节流
        /// </summary>
        private readonly Stream _stream;

        /// <summary>
        /// 串行化写操作的同步对象
        /// </summary>
        private readonly object _sendSync = new object();

        /// <summary>
        /// 连接是否已关闭；关闭后发送直接丢弃
        /// </summary>
        private bool _closed;

        /// <summary>
        /// 是否 TLS 连接
        /// </summary>
        private readonly bool _isTls;

        /// <summary>
        /// 包装一条已握手的字节流
        /// </summary>
        /// <param name="stream">双向字节流</param>
        /// <param name="localEndPoint">连接本地端点</param>
        /// <param name="remoteEndPoint">连接对端端点</param>
        /// <param name="isTls">是否为 TLS 连接</param>
        /// <exception cref="ArgumentNullException">任一参数为 null</exception>
        public StreamTurnControlChannel(Stream stream, IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, bool isTls)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }
            if (localEndPoint == null)
            {
                throw new ArgumentNullException(nameof(localEndPoint));
            }
            if (remoteEndPoint == null)
            {
                throw new ArgumentNullException(nameof(remoteEndPoint));
            }

            _stream = stream;
            _isTls = isTls;
            LocalEndPoint = localEndPoint;
            RemoteEndPoint = remoteEndPoint;
        }

        /// <summary>
        /// 连接对端端点
        /// </summary>
        public IPEndPoint RemoteEndPoint { get; private set; }

        /// <summary>
        /// 连接本地端点
        /// </summary>
        public IPEndPoint LocalEndPoint { get; private set; }

        /// <summary>
        /// 流式连接，ChannelData 必须补齐到 4 字节倍数
        /// </summary>
        public bool IsStream
        {
            get { return true; }
        }

        /// <summary>
        /// 是否为 TLS 连接；由监听器在建立连接时确定
        /// </summary>
        public bool IsTls
        {
            get { return _isTls; }
        }

        /// <summary>
        /// 接管为数据连接时使用的双向字节流，与控件连接共用同一条流
        /// </summary>
        public Stream DataStream
        {
            get { return _stream; }
        }

        /// <summary>
        /// 把一条完整报文写回连接；写失败只标记关闭，不向上抛
        /// </summary>
        /// <param name="data">待发送的完整报文</param>
        public void Send(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return;
            }

            lock (_sendSync)
            {
                if (_closed)
                {
                    return;
                }

                try
                {
                    _stream.Write(data, 0, data.Length);
                    _stream.Flush();
                }
                catch (Exception ex)
                {
                    // 对端已经断开时写失败属于正常收尾，不把异常抛回 TURN 处理路径
                    _closed = true;
                    P2PLogHelper.Debug("TurnServer", "TLS 控制连接回送失败：" + ex.GetType().Name);
                }
            }
        }
    }
}