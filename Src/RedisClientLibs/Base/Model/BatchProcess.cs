/****************************************************************************
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.Sockets.Model
*文件名： Batch
*版本号： V1.0.0.0
*唯一标识：abb889b6-5653-4420-9038-d77cdfbc7f47
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/7 11:01:12
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/7 11:01:12
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.Sockets.Model
{
    /// <summary>
    /// 批量处理类
    /// </summary>
    public class BatchProcess<T> : ISyncBase, IDisposable
    {
        object _syncLocker = new object();

        public object SyncLocker
        {
            get
            {
                return _syncLocker;
            }
        }

        bool _disposed = false;

        ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();

        public BatchProcess(Action<List<T>> callBack, int maxNum = 500, int maxTime = 500)
        {
            ThreadHelper.Run(() =>
            {
                while (!_disposed)
                {
                    var running = true;

                    var list = new List<T>();

                    var task = Task.Factory.StartNew(() =>
                    {
                        while (running && list.Count < maxNum)
                        {
                            if (_queue.TryDequeue(out T t))
                            {
                                if (t != null)
                                {
                                    list.Add(t);
                                }
                                else
                                {
                                    ThreadHelper.Sleep(1);
                                }
                            }
                            else
                                ThreadHelper.Sleep(1);
                        }
                    });

                    Task.WaitAll(new Task[] { task }, maxTime);

                    running = false;

                    callBack?.Invoke(list);

                    list.Clear();
                    list = null;
                }
            }, true, System.Threading.ThreadPriority.Highest);
        }

        public void Package(T t)
        {
            _queue.Enqueue(t);
        }


        public void Dispose()
        {
            _disposed = true;
            //_queue.Clear();
            _queue = null;
        }
    }
}
