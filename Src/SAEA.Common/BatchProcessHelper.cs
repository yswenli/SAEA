/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.Commom
*文件名： BatchProcessHelper
*版本号： V3.3.3.4
*唯一标识：bf3043aa-a84d-42ab-a6b6-b3adf2ab8925
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/9/10 16:53:26
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/9/10 16:53:26
*修改人： yswenli
*版本号： V3.3.3.4
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace SAEA.Common
{
    /// <summary>
    /// 批量间隔处理任务
    /// </summary>
    public class BatchProcessHelper<T> : IDisposable
    {
        List<T> list = new List<T>();

        object syncObj = new object();

        private bool isDisposed = false;
        public bool IsDisposed { get => isDisposed; set => isDisposed = value; }

        AutoResetEvent autoResetEvent = new AutoResetEvent(false);

        int _maxCount = 1000;


        public BatchProcessHelper(Action<T[]> action, int num = 1000) : this(num, new TimeSpan(0, 0, 0, 0, 200), action) { }

        public BatchProcessHelper(int num, TimeSpan timeSpan, Action<T[]> action, int maxCount = 1000)
        {
            _maxCount = maxCount;
            DoLoop(num, timeSpan, action);
        }

        private void DoLoop(int num, TimeSpan timeSpan, Action<T[]> action)
        {
            var td = new Thread(new ThreadStart(() =>
            {
                var dt = DateTimeHelper.Now;
                while (!IsDisposed)
                {
                    try
                    {
                        if (list.Count == num || (list.Count > 0 && dt.AddMilliseconds(timeSpan.TotalMilliseconds) <= DateTimeHelper.Now))
                        {
                            lock (syncObj)
                            {
                                action?.Invoke(list.ToArray());
                                list.Clear();
                                dt = DateTimeHelper.Now;
                                continue;
                            }
                        }
                        else
                        {
                            autoResetEvent.WaitOne(10);
                        }
                    }
                    catch
                    {
                        Dispose();
                        return;
                    }
                }
            }));
            td.IsBackground = true;
            td.Start();
        }

        public void Post(T t)
        {
            lock (syncObj)
            {
                if (!isDisposed)
                {
                    if (list.Count >= _maxCount)
                    {
                        autoResetEvent.WaitOne(2000);
                    }
                    list.Add(t);
                }
            }
        }

        public void Post(T[] ts)
        {
            lock (syncObj)
            {
                if (!isDisposed)
                {
                    if (list.Count >= _maxCount)
                    {
                        autoResetEvent.WaitOne(2000);
                    }
                    list.AddRange(ts);
                }
            }
        }

        public void Dispose()
        {
            isDisposed = true;
            list.Clear();
            autoResetEvent.Close();
        }

    }
}
