/****************************************************************************
*项目名称：SAEA.Common
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Common
*类 名 称：TaskFollow
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/7/27 16:11:35
*描述：
*=====================================================================
*修改时间：2020/7/27 16:11:35
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace SAEA.Common
{
    /// <summary>
    /// 任务流
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class TaskFollow<T>
    {
        ConcurrentQueue<TaskItem<T>> _operationCache;

        ConcurrentDictionary<Guid, TaskResultItem<T>> _resultCache;

        /// <summary>
        /// 任务流
        /// </summary>
        public TaskFollow()
        {
            _operationCache = new ConcurrentQueue<TaskItem<T>>();

            _resultCache = new ConcurrentDictionary<Guid, TaskResultItem<T>>();

            ThreadHelper.Run(() =>
            {
                while (true)
                {
                    if (!_operationCache.IsEmpty && _operationCache.TryDequeue(out TaskItem<T> taskItem))
                    {
                        T result = default(T);
                        try
                        {
                            result = taskItem.Func.Invoke();

                            _resultCache.TryAdd(taskItem.Guid, new TaskResultItem<T>(taskItem.Guid, result, null, true));
                        }
                        catch (Exception ex)
                        {
                            _resultCache.TryAdd(taskItem.Guid, new TaskResultItem<T>(taskItem.Guid, result, ex, false));
                        }
                    }
                    else
                    {
                        ThreadHelper.Sleep(1);
                    }
                }
            });
        }
        /// <summary>
        /// 添加任务
        /// </summary>
        /// <param name="func"></param>s
        public T Push(Func<T> func)
        {
            var guid = Guid.NewGuid();

            _operationCache.Enqueue(new TaskItem<T>(guid, func));

            while (!_resultCache.ContainsKey(guid))
            {
                ThreadHelper.Sleep(1);
            }
            if (_resultCache.TryRemove(guid, out TaskResultItem<T> result))
            {
                if (result.Status)
                {
                    return result.Result;
                }
                else
                {
                    throw result.Exception;
                }
            }
            return default(T);
        }

    }

    public class TaskItem<T>
    {
        public Guid Guid { get; set; }

        public Func<T> Func { get; set; }

        public TaskItem(Guid guid, Func<T> func)
        {
            Guid = guid;
            Func = func;
        }
    }

    public class TaskResultItem<T>
    {
        public Guid Guid { get; set; }

        public T Result { get; set; }

        public Exception Exception { get; set; }

        public bool Status { get; set; } = false;

        public TaskResultItem(Guid guid)
        {
            Guid = guid;
        }

        public TaskResultItem(Guid guid, T result, Exception ex, bool status)
        {
            Guid = guid;
            Result = result;
            Exception = ex;
            Status = status;
        }
    }
}
