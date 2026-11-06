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
*命名空间：SAEA.MQTT.Internal
*文件名： AsyncQueueDequeueResult
*版本号： v26.4.23.1
*唯一标识：845a8d58-ca4c-497b-aa6c-3a110b67582e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：AsyncQueueDequeueResult结果类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:04:26
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.MQTT.Internal
{
    /// <summary>
    /// 异步队列的出队结果
    /// </summary>
    /// <typeparam name="TItem">队列元素类型</typeparam>
    public class AsyncQueueDequeueResult<TItem>
    {
        /// <summary>
        /// 创建出队结果
        /// </summary>
        /// <param name="isSuccess">是否成功取出元素</param>
        /// <param name="item">取出的元素，失败时传元素类型的默认值</param>
        public AsyncQueueDequeueResult(bool isSuccess, TItem item)
        {
            IsSuccess = isSuccess;
            Item = item;
        }

        /// <summary>
        /// 是否成功取出元素
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// 取出的元素，IsSuccess 为 false 时为其类型的默认值
        /// </summary>
        public TItem Item { get; }
    }
}