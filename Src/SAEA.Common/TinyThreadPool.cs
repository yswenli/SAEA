/****************************************************************************
*项目名称：SAEA.Common
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Common
*类 名 称：TinyThreadPool
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/10/12 16:54:35
*描述：
*=====================================================================
*修改时间：2019/10/12 16:54:35
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace SAEA.Common
{
    /// <summary>
    /// 轻量级线程池
    /// </summary>
    public class TinyThreadPool : IDisposable
    {
        /// <summary>
        /// TinyThreadPool异常委托类
        /// </summary>
        /// <param name="imThreadPoolException"></param>
        public delegate void TinyThreadPoolOnErrorHandler(TinyThreadPoolException tinyThreadPoolException);

        /// <summary>
        /// 执行前
        /// </summary>
        /// <returns></returns>
        public delegate bool TinyThreadPoolOnInvoking();
        /// <summary>
        /// 执行后
        /// </summary>
        public delegate void TinyThreadPoolOnInvoked();

        int _maxWorkCount = 100;

        int _maxQueueCount = 5000 * 1000;

        ConcurrentQueue<Action> _queue = null;

        Semaphore _maxWorkSemaphore = null;

        Semaphore _maxQueueSemaphore = null;

        AutoResetEvent _nullProcess = null;

        bool _started = false;

        /// <summary>
        /// 线程池已启动
        /// </summary>
        public bool Started
        {
            get { return _started; }

        }

        object _locker = new object();

        Thread _workThread = null;

        /// <summary>
        /// 执行任务发生异常时事件
        /// </summary>
        public event TinyThreadPoolOnErrorHandler OnError;

        /// <summary>
        /// 任务执行前
        /// </summary>
        public event TinyThreadPoolOnInvoking OnInvoking;
        /// <summary>
        /// 任务执行后
        /// </summary>
        public event TinyThreadPoolOnInvoked OnInvoked;

        int workingThreadCount = 0;

        /// <summary>
        /// 当前正在工作的线程数
        /// </summary>
        public int WorkingThreadCount
        {
            get
            {
                return workingThreadCount;
            }
        }

        bool _disposed = false;

        /// <summary>
        /// 是否已释放
        /// </summary>
        public bool Disposed
        {
            get
            {
                return _disposed;
            }
        }

        /// <summary>
        /// 自定义线程池
        /// </summary>
        /// <param name="maxWorkCount">最大执行线程</param>
        /// <param name="maxQueueCount">最大排队任务</param>
        public TinyThreadPool(int maxWorkCount = 100, int maxQueueCount = 5000 * 1000)
        {
            _maxWorkCount = maxWorkCount;
            _maxQueueCount = maxQueueCount;

            _maxWorkSemaphore = new Semaphore(_maxWorkCount, _maxWorkCount);
            _maxQueueSemaphore = new Semaphore(_maxQueueCount, _maxQueueCount);
            _nullProcess = new AutoResetEvent(false);

            _queue = new ConcurrentQueue<Action>();

        }

        /// <summary>
        /// 添加新的操作到队列中，如果队列已满则阻塞
        /// </summary>
        /// <param name="work"></param>
        public void BlockAdd(Action work)
        {
            _maxQueueSemaphore.WaitOne();
            _queue.Enqueue(work);
        }

        /// <summary>
        /// 启动线程池
        /// </summary>
        public void Start()
        {
            lock (_locker)
            {
                if (!_started)
                {
                    _started = true;

                    RunWorkThread();
                }
            }
        }

        /// <summary>
        /// 关闭线程池
        /// </summary>
        public void Stop()
        {
            _started = false;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _started = false;

            if (_workThread != null && _workThread.IsAlive)
            {
                _workThread.Abort();
            }

            while (_queue != null && !_queue.IsEmpty)
            {
                _queue.TryDequeue(out Action action);
            }

            _queue = null;

            _workThread = null;

            _disposed = true;
        }

        void RunWorkThread()
        {
            _workThread = new Thread(new ThreadStart(() =>
            {
                while (_started)
                {
                    if (_queue != null && _queue.TryDequeue(out Action work))
                    {
                        _maxQueueSemaphore.Release();

                        _maxWorkSemaphore.WaitOne();

                        var th = new Thread(new ThreadStart(() =>
                        {
                            Interlocked.Increment(ref workingThreadCount);
                            try
                            {
                                var result = OnInvoking?.Invoke();

                                if (!result.HasValue || result.Value)
                                {
                                    work.Invoke();

                                    OnInvoked?.Invoke();
                                }
                            }
                            catch (Exception ex)
                            {
                                OnError?.Invoke(new TinyThreadPoolException(ex));
                            }
                            finally
                            {
                                Interlocked.Decrement(ref workingThreadCount);
                                _maxWorkSemaphore.Release();
                            }

                        }));
                        th.Start();
                    }
                    else
                    {
                        _nullProcess.WaitOne(10);
                    }
                }
            }));
            _workThread.IsBackground = true;
            _workThread.Start();
        }

        public class TinyThreadPoolException : Exception
        {
            public TinyThreadPoolException(Exception ex) : base(ex.Message) { }
        }


        #region static

        static Lazy<TinyThreadPool> _ThreadPool = null;

        /// <summary>
        /// 静态创建一个公共线程池
        /// </summary>
        /// <param name="maxWorkCount"></param>
        /// <param name="maxQueueCount"></param>
        /// <returns></returns>
        public static TinyThreadPool Create(int maxWorkCount = 2, int maxQueueCount = 100)
        {
            if (_ThreadPool == null)
            {
                _ThreadPool = new Lazy<TinyThreadPool>(() => new TinyThreadPool(maxWorkCount, maxQueueCount));
            }
            return _ThreadPool.Value;
        }
        /// <summary>
        /// 使用公共线程池运行
        /// </summary>
        /// <param name="action"></param>
        /// <param name="maxWorkCount"></param>
        /// <param name="maxQueueCount"></param>
        public static void Run(Action action, int maxWorkCount = 2, int maxQueueCount = 100)
        {
            var threadPool = Create(maxWorkCount, maxQueueCount);

            threadPool.BlockAdd(action);
        }

        #endregion
    }
}
