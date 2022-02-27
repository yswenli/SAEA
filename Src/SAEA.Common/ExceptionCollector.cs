/****************************************************************************
*Copyright (c) 2018-2022yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Commom
*文件名： ExceptionCollector
*版本号： v7.0.0.1
*唯一标识：0957f3bb-7462-4ff0-867d-0a8c9411f2eb
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/12 9:33:39
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/12 9:33:39
*修改人： yswenli
*版本号： v7.0.0.1
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
