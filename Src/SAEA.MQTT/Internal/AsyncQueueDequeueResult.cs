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
*唯一标识：a78b7cb2-eedf-41aa-af8f-a5d26127c613
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MQTT内部工具类
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MQTT内部工具类
*
*****************************************************************************/
namespace SAEA.MQTT.Internal
{
    public class AsyncQueueDequeueResult<TItem>
    {
        public AsyncQueueDequeueResult(bool isSuccess, TItem item)
        {
            IsSuccess = isSuccess;
            Item = item;
        }

        public bool IsSuccess { get; }

        public TItem Item { get; }
    }
}