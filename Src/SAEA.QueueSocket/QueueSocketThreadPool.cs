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
*命名空间：SAEA.QueueSocket
*文件名： QueueSocketThreadPool
*版本号： v26.11.5.1
*唯一标识：52d6fc54-7921-436f-9658-cb3e4b37bbcc
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/9/23 16:49:22
*描述：QueueSocket 线程池最小线程数预热
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 13:11:19
*修改人： yswenli
*版本号： v26.11.5.1
*描述：规范文件头 banner
*
*****************************************************************************/
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