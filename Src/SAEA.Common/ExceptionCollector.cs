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
*命名空间：SAEA.Common
*文件名： ExceptionCollector
*版本号： v26.4.23.1
*唯一标识：f569a53a-39ad-4c63-b87f-6f83d8d58245
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/08/24 16:31:11
*描述：ExceptionCollector接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:45
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;

using SAEA.Common.Threading;

namespace SAEA.Common
{
    /// <summary>
    /// 异常收集器
    /// </summary>
    public static class ExceptionCollector
    {
        static bool _isClose = false;

        /// <summary>
        /// 异常处理委托
        /// </summary>
        /// <param name="name">异常所属名称</param>
        /// <param name="ex">被捕获的异常</param>
        public delegate void OnErrHander(string name, Exception ex);

        /// <summary>
        /// 异常出队时触发，在后台轮询线程上调用，订阅方需自行保证处理不阻塞
        /// </summary>
        public static event OnErrHander OnErr;

        static ConcurrentDictionary<string, ConcurrentQueue<Exception>> _eDic = new ConcurrentDictionary<string, ConcurrentQueue<Exception>>();

        /// <summary>
        /// 异常收集器
        /// </summary>
        static ExceptionCollector()
        {
            TaskHelper.Run(() =>
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
            });
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