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
*文件名： ObjectAsyncLock
*版本号： v26.4.23.1
*唯一标识：a1b2c3d4-e5f6-7890-abcd-ef1234567890
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2023/10/13 16:43:23
*描述：ObjectAsyncLock锁类，提供异步锁功能
*
*=====================================================================
*修改标记
*修改时间：2023/10/13 16:43:23
*修改人： yswenli
*版本号： v26.4.23.1
*描述：ObjectAsyncLock锁类，提供异步锁功能
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
