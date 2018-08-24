/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.Commom
*文件名： SyncHelper
*版本号： V1.0.0.0
*唯一标识：31743e53-5af7-48fb-b248-e1b3504b9c68
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/17 17:26:35
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/17 17:26:35
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace SAEA.Common
{
    /// <summary>
    /// 方法、事件同步
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SyncHelper<T>
    {
        ConcurrentDictionary<long, SyncInfo<T>> _cmdDic = new ConcurrentDictionary<long, SyncInfo<T>>();

        /// <summary>
        /// 设置等待点
        /// </summary>
        /// <param name="sNo"></param>
        /// <param name="callBack"></param>
        /// <param name="millisecondsTimeout"></param>
        public bool Wait(long sNo, Action work, Action<T> callBack, int millisecondsTimeout = 180 * 1000)
        {
            var result = false;

            var si = new SyncInfo<T>() { Action = callBack };
            _cmdDic.TryAdd(sNo, si);

            work?.Invoke();
            if (millisecondsTimeout > 0)
                result = si.AutoResetEvent.WaitOne(millisecondsTimeout);
            else
                result = si.AutoResetEvent.WaitOne();
            si.AutoResetEvent.Close();
            si.Action = null;
            return result;
        }

        /// <summary>
        /// 通知取消等待
        /// </summary>
        /// <param name="sNo"></param>
        /// <param name="t"></param>
        public void Set(long sNo, T t)
        {
            if (_cmdDic.TryRemove(sNo, out SyncInfo<T> si))
            {
                si.Action.Invoke(t);
                si.AutoResetEvent.Set();
            }
            else
            {
                Thread.Sleep(1);
                Set(sNo, t);
            }
        }
    }

    internal class SyncInfo<T>
    {
        public AutoResetEvent AutoResetEvent { get; set; } = new AutoResetEvent(false);

        public Action<T> Action { get; set; }
    }
}
