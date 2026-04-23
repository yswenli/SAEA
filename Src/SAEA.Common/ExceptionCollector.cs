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
*唯一标识：b497f3e3-f1e7-4fa7-81cb-f0c6a6be9aee
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/08/24 16:31:11
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/08/24 16:31:11
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
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

        public delegate void OnErrHander(string name, Exception ex);

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