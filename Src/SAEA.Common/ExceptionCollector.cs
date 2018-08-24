using System;
using System.Collections.Concurrent;

namespace SAEA.Common
{
    /// <summary>
    /// 异常收集器
    /// </summary>
    public static class ExceptionCollector
    {
        static bool _isClose = false;

        public delegate void OnErrHander(string name, Exception ex);

        public static event OnErrHander OnErr;

        static ConcurrentDictionary<string, ConcurrentQueue<Exception>> _eDic = new ConcurrentDictionary<string, ConcurrentQueue<Exception>>();

        /// <summary>
        /// 异常收集器
        /// </summary>
        static ExceptionCollector()
        {
            ThreadHelper.Run(() =>
            {
                while (!_isClose)
                {
                    foreach (var item in _eDic)
                    {
                        if (item.Value.TryDequeue(out Exception ex))
                        {
                            OnErr?.Invoke(item.Key, ex);
                        }
                    }
                    ThreadHelper.Sleep(50);
                }
            }, true, System.Threading.ThreadPriority.Highest);
        }

        /// <summary>
        /// 添加exception
        /// </summary>
        /// <param name="name"></param>
        /// <param name="ex"></param>
        public static void Add(string name, Exception ex)
        {
            var queue = _eDic.GetOrAdd(name, new ConcurrentQueue<Exception>());

            queue.Enqueue(ex);
        }
    }
}
