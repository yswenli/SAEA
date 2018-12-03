/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.Commom
*文件名： TaskHelper
*版本号： V3.3.3.5
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
*版本号： V3.3.3.5
*描述：
*
*****************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Common
{
    /// <summary>
    /// 任务辅助类
    /// </summary>
    public static class TaskHelper
    {
        /// <summary>
        /// 超时取消任务
        /// </summary>
        /// <param name="action"></param>
        /// <param name="timeOut"></param>
        /// <param name="canceled"></param>
        public static async void WaitFor(Action action, int timeOut, Action canceled)
        {
            var cts = new CancellationTokenSource(timeOut);
            cts.Token.Register(canceled);
            await Task.Factory.StartNew(action, cts.Token);
        }

        public static Task Start(Action action)
        {
            return Task.Factory.StartNew(action);
        }
    }
}
