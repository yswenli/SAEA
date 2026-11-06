/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.Common.EventBus
*文件名： EventSubscriber
*版本号： v26.11.5.1
*唯一标识：7ec51b96-5e2c-430c-b2e6-c50cc33ba679
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 12:40:36
*描述：事件订阅者
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
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Common.EventBus
{
    /// <summary>
    /// 事件总线订阅者
    /// </summary>
    public class EventSubscriber
    {
        /// <summary>
        /// 订阅者标识，构造时自动生成的 32 位 GUID 字符串
        /// </summary>
        public string Id { get; }
        /// <summary>
        /// 消息处理委托，参数为消息与取消令牌
        /// </summary>
        public Func<EventMessage, CancellationToken, Task> Handler { get; }
        /// <summary>
        /// 处理失败的最大重试次数
        /// </summary>
        public int RetryCount { get; }
        /// <summary>
        /// 重试间隔，为 null 时由调用方决定
        /// </summary>
        public TimeSpan? RetryDelay { get; }

        /// <summary>
        /// 创建订阅者并生成唯一标识
        /// </summary>
        /// <param name="handler">消息处理委托，不能为 null</param>
        /// <param name="retryCount">处理失败的最大重试次数</param>
        /// <param name="retryDelay">重试间隔，可为 null</param>
        public EventSubscriber(
            Func<EventMessage, CancellationToken, Task> handler,
            int retryCount = 3,
            TimeSpan? retryDelay = null)
        {
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
            RetryCount = retryCount;
            RetryDelay = retryDelay;
            Id = Guid.NewGuid().ToString("N");
        }
    }
}