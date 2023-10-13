/****************************************************************************
*Copyright (c) 2023 RiverLand All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WALLE
*公司名称：河之洲
*命名空间：SAEA.Common
*文件名： ObjectAsyncLock
*版本号： V1.0.0.0
*唯一标识：c5a2d6f5-1331-4dff-a0f0-2b7c34692140
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：walle.wen@tjingcai.com
*创建时间：2023/10/13 16:43:23
*描述：异步对象锁
*
*=================================================
*修改标记
*修改时间：2023/10/13 16:43:23
*修改人： yswenli
*版本号： V1.0.0.0
*描述：异步对象锁
*
*****************************************************************************/
using System;
using System.Threading;
using System.Threading.Tasks;

using Nito.AsyncEx;

namespace SAEA.Common
{
    /// <summary>
    /// 异步对象锁
    /// </summary>
    public static class ObjectAsyncLock
    {
        /// <summary>
        /// 异步锁
        /// </summary>
        /// <param name="action"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task AsyncLock(Action action, CancellationToken cancellationToken)
        {
            var _mutex = new AsyncLock();
            using (await _mutex.LockAsync(cancellationToken))
            {
                action.Invoke();
            }
        }
        /// <summary>
        /// 异步锁
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public static async Task AsyncLock(Action action) => await AsyncLock(action, CancellationToken.None);

        /// <summary>
        /// 异步锁
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<T> AsyncLock<T>(Func<Task<T>> func, CancellationToken cancellationToken)
        {
            var _mutex = new AsyncLock();
            using (await _mutex.LockAsync(cancellationToken))
            {
                return await func.Invoke();
            }
        }

        /// <summary>
        /// 异步锁
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        /// <returns></returns>
        public static async Task<T> AsyncLock<T>(Func<Task<T>> func) => await AsyncLock<T>(func, CancellationToken.None);
    }
}
