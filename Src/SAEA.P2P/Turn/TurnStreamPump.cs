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
*命名空间：SAEA.P2P.Turn
*文件名： TurnStreamPump
*版本号： v26.11.5.1
*唯一标识：86569703-08ca-4ddf-8e3f-209aa08b6049
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 04:10:00
*描述：TURN 客户端流式读泵
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 04:10:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN 客户端流式读泵
*
*****************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Common;

namespace SAEA.P2P.Turn
{
    /// <summary>
    /// TURN 客户端流式读泵：按块读取字节流，交给分帧器自定界后逐帧回调。
    /// TCP 与 TLS 只在读取委托上不同，读循环、分帧与退出策略共用这一份实现。
    /// 回调只拿到整帧副本；读取缓冲与分帧器都由本类持有，不会跨线程共享。
    /// </summary>
    internal sealed class TurnStreamPump : IDisposable
    {
        /// <summary>
        /// 单次读取的最大字节数；半包状态由分帧器跨读取保留，不依赖本次读取的边界
        /// </summary>
        private const int ReceiveBufferSize = 8192;

        /// <summary>
        /// 停止时等待读循环退出的上限，单位毫秒；底层读不响应取消时不至于让释放方永久阻塞
        /// </summary>
        private const int StopWaitMilliseconds = 2000;

        /// <summary>
        /// 从底层连接读取一次字节，第二个参数是最大读取长度，返回不大于 0 表示对端已关闭
        /// </summary>
        private readonly Func<byte[], int, CancellationToken, Task<int>> _readAsync;

        /// <summary>
        /// 每解析出一条完整帧时的回调
        /// </summary>
        private readonly Action<byte[]> _onFrame;

        /// <summary>
        /// 复用的读取缓冲，避免每次读取都分配
        /// </summary>
        private readonly byte[] _buffer = new byte[ReceiveBufferSize];

        /// <summary>
        /// 流式分帧器；只在读循环线程上使用
        /// </summary>
        private readonly TurnStreamFramer _framer = new TurnStreamFramer();

        /// <summary>
        /// 终止读循环的令牌源
        /// </summary>
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();

        /// <summary>
        /// 保护启动与释放的同步对象
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 读循环任务；尚未启动或已释放时为 null
        /// </summary>
        private Task _loop;

        /// <summary>
        /// 读循环是否仍在运行
        /// </summary>
        private volatile bool _running;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 创建读泵；构造阶段不启动读循环
        /// </summary>
        /// <param name="readAsync">从底层连接读取一次字节；第二个参数是最大读取长度</param>
        /// <param name="onFrame">每解析出一条完整帧时的回调，不得为 null</param>
        /// <exception cref="ArgumentNullException">任一参数为 null</exception>
        public TurnStreamPump(Func<byte[], int, CancellationToken, Task<int>> readAsync, Action<byte[]> onFrame)
        {
            if (readAsync == null)
            {
                throw new ArgumentNullException(nameof(readAsync));
            }
            if (onFrame == null)
            {
                throw new ArgumentNullException(nameof(onFrame));
            }

            _readAsync = readAsync;
            _onFrame = onFrame;
        }

        /// <summary>
        /// 读循环是否仍在运行；读循环退出或尚未启动时为 false
        /// </summary>
        public bool IsRunning
        {
            get { return _running; }
        }

        /// <summary>
        /// 启动读循环；重复调用不会启动第二个循环
        /// </summary>
        public void Start()
        {
            lock (_sync)
            {
                if (_disposed || _loop != null)
                {
                    return;
                }

                // 先置位再派发，避免启动后立刻查询 IsRunning 时读到 false
                _running = true;
                _loop = Task.Run(() => RunAsync(_cancellation.Token));
            }
        }

        /// <summary>
        /// 取消读循环并等待其退出，最多等待 <see cref="StopWaitMilliseconds"/> 毫秒；可重复调用
        /// </summary>
        public void Stop()
        {
            Task loop;
            lock (_sync)
            {
                loop = _loop;
            }

            try
            {
                _cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // 已释放时取消为空操作
            }

            if (loop == null)
            {
                return;
            }

            // 在读循环内部（例如帧回调里）调用时不能等待自己，否则必然白等到超时
            if (loop.Id == Task.CurrentId)
            {
                return;
            }

            try
            {
                loop.Wait(StopWaitMilliseconds);
            }
            catch (AggregateException)
            {
                // 读循环自行吞掉异常，这里只是兜住等待期间的包装异常
            }
            catch (ObjectDisposedException)
            {
                // 任务已结束时等待可能抛该异常，无需处理
            }
        }

        /// <summary>
        /// 停止读循环并释放令牌源；可重复调用
        /// </summary>
        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
            }

            Stop();

            try
            {
                _cancellation.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // 已释放时忽略
            }
        }

        /// <summary>
        /// 读循环：读一块、分帧、逐帧回调，直到取消、对端关闭或读取异常
        /// </summary>
        /// <param name="cancellationToken">终止令牌</param>
        /// <returns>读循环结束的任务</returns>
        private async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    int read;
                    try
                    {
                        read = await _readAsync(_buffer, ReceiveBufferSize, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // 取消与对端断开都走这里，只记调试日志，不向调用方抛
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            P2PLogHelper.Debug("TurnStreamPump", "流式读循环结束：" + ex.GetType().Name);
                        }
                        return;
                    }

                    if (read <= 0)
                    {
                        return;
                    }

                    Dispatch(read);
                }
            }
            catch (Exception ex)
            {
                // 分帧或回调抛出的异常同样只结束本循环，避免拖垮调用方的线程
                P2PLogHelper.Debug("TurnStreamPump", "流式读循环异常退出：" + ex.GetType().Name);
            }
            finally
            {
                _running = false;
            }
        }

        /// <summary>
        /// 把本次读到的字节喂给分帧器，并逐帧回调；只在同步方法内使用读取缓冲的视图
        /// </summary>
        /// <param name="count">本次读到的字节数</param>
        private void Dispatch(int count)
        {
            _framer.Feed(new ReadOnlySpan<byte>(_buffer, 0, count));

            TurnStreamFrame frame;
            while (_framer.TryReadFrame(out frame))
            {
                // 帧数据由分帧器按帧新建，回调方可以跨回调继续持有
                _onFrame(frame.Data);
            }
        }
    }
}
