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
*文件名： TurnStreamSession
*版本号： v26.11.5.1
*唯一标识：5787d80a-58f9-470d-82ba-9e3c046687bf
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 02:30:00
*描述：TCP 与 TLS 控制连接共用的流式会话循环
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 02:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TCP 与 TLS 控制连接共用的流式会话循环
*
*****************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Common;
using SAEA.P2P.Stun;
using SAEA.P2P.Turn;

namespace SAEA.P2P.TurnServer
{
    /// <summary>
    /// 流式控制连接的会话循环：只负责读取、分帧与分派，连接断开时释放该连接上的全部分配。
    /// TCP 与 TLS 只在读取方式上不同，读取委托由监听器注入。
    /// </summary>
    internal sealed class TurnStreamSession
    {
        /// <summary>
        /// 单次读取的最大字节数；分帧器保留半包状态，不依赖本次读取的边界
        /// </summary>
        private const int ReceiveBufferSize = 8192;

        /// <summary>
        /// 共用的 TURN 服务端
        /// </summary>
        private readonly TurnServer _server;

        /// <summary>
        /// 帧所属的控制连接
        /// </summary>
        private readonly ITurnControlChannel _control;

        /// <summary>
        /// 从底层连接读取一次字节
        /// </summary>
        private readonly Func<byte[], Task<int>> _readAsync;

        /// <summary>
        /// 复用的读取缓冲，避免每条连接每次读取都分配
        /// </summary>
        private readonly byte[] _buffer = new byte[ReceiveBufferSize];

        /// <summary>
        /// 流式分帧器；半包状态跨多次读取保留
        /// </summary>
        private readonly TurnStreamFramer _framer = new TurnStreamFramer();

        /// <summary>
        /// 是否已经开始执行，保证释放只做一次
        /// </summary>
        private int _started;

        /// <summary>
        /// 创建会话循环
        /// </summary>
        /// <param name="server">共用的 TURN 服务端</param>
        /// <param name="control">帧所属的控制连接</param>
        /// <param name="readAsync">从底层连接读取一次字节；返回不大于 0 表示连接已关闭</param>
        /// <exception cref="ArgumentNullException">任一参数为 null</exception>
        public TurnStreamSession(TurnServer server, ITurnControlChannel control, Func<byte[], Task<int>> readAsync)
        {
            if (server == null)
            {
                throw new ArgumentNullException(nameof(server));
            }
            if (control == null)
            {
                throw new ArgumentNullException(nameof(control));
            }
            if (readAsync == null)
            {
                throw new ArgumentNullException(nameof(readAsync));
            }

            _server = server;
            _control = control;
            _readAsync = readAsync;
        }

        /// <summary>
        /// 运行读取循环直到连接关闭；退出时释放该控制连接上的全部 allocation
        /// </summary>
        /// <returns>循环结束的任务</returns>
        public async Task RunAsync()
        {
            // 同一条连接只允许一个读循环，重复调用直接返回，避免并发释放同一批分配
            if (Interlocked.Exchange(ref _started, 1) != 0)
            {
                return;
            }

            try
            {
                while (true)
                {
                    int read;
                    try
                    {
                        read = await _readAsync(_buffer).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        P2PLogHelper.Debug("TurnStreamSession", "流式控制连接读取结束：" + ex.GetType().Name);
                        break;
                    }

                    if (read <= 0)
                    {
                        break;
                    }

                    Dispatch(read);
                }
            }
            finally
            {
                // 连接断开必须回收该连接上的 allocation，否则中继 socket 与端口会一直泄漏
                _server.ReleaseControlChannel(_control);
            }
        }

        /// <summary>
        /// 把本次读到的字节喂给分帧器，并逐帧分派给 TURN 服务端
        /// </summary>
        /// <param name="count">本次读到的字节数</param>
        private void Dispatch(int count)
        {
            // 分帧器只消费本次读取的视图，跨读取的半包状态由它自己维护
            _framer.Feed(new ReadOnlySpan<byte>(_buffer, 0, count));

            TurnStreamFrame frame;
            while (_framer.TryReadFrame(out frame))
            {
                if (frame.Kind == TurnStreamFrameKind.ChannelData)
                {
                    _server.HandleStreamChannelData(frame.Data, _control);
                    continue;
                }

                StunMessage message;
                StunParseError error;
                if (!StunMessage.TryParse(frame.Data, out message, out error))
                {
                    // 首字节与长度都对得上但内容非法时无法再重同步，丢弃这一帧
                    P2PLogHelper.Debug("TurnStreamSession", "丢弃无法解析的 STUN 报文：" + error);
                    continue;
                }

                _server.HandleStreamRequest(message, _control);
            }
        }
    }
}