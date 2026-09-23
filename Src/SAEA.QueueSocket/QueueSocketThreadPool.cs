using System.Threading;

namespace SAEA.QueueSocket
{
    /// <summary>
    /// 为 QueueSocket 客户端与服务端一次性抬高线程池最小工作线程数。
    /// <para>
    /// 批量发送回调与部分发送路径运行在线程池工作项上，而默认最小工作线程数等于 CPU 核心数，
    /// 突发流量下会被瞬间占满，CLR 只能每秒补一个线程，期间表现为数秒级的 0 吞吐停顿。
    /// 这里在首次创建 QClient/QServer 时把最小工作线程数抬到安全水位，避免该停顿。
    /// </para>
    /// </summary>
    internal static class QueueSocketThreadPool
    {
        const int MinWorkerThreads = 256;

        static int _configured;

        internal static void EnsureConfigured()
        {
            if (Interlocked.Exchange(ref _configured, 1) != 0) return;

            ThreadPool.GetMinThreads(out var worker, out var io);
            if (worker >= MinWorkerThreads) return;

            ThreadPool.SetMinThreads(MinWorkerThreads, io);
        }
    }
}