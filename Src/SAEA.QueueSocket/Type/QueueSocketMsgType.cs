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
*命名空间：SAEA.QueueSocket.Type
*文件名： QueueSocketMsgType
*版本号： v26.4.23.1
*唯一标识：a991172f-4007-4062-b094-6c99f200c104
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：QueueSocketMsgType类型枚举
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:31
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.QueueSocket.Type
{
    /// <summary>
    /// 队列协议消息类型
    /// </summary>
    public enum QueueSocketMsgType : byte
    {
        /// <summary>
        /// 心跳请求
        /// </summary>
        Ping = 1,
        /// <summary>
        /// 心跳响应
        /// </summary>
        Pong = 2,
        /// <summary>
        /// 发布消息到主题
        /// </summary>
        Publish = 3,
        /// <summary>
        /// 订阅主题
        /// </summary>
        Subcribe = 4,
        /// <summary>
        /// 取消订阅主题
        /// </summary>
        Unsubcribe = 5,
        /// <summary>
        /// 关闭连接
        /// </summary>
        Close = 6,
        /// <summary>
        /// 主题数据推送
        /// </summary>
        Data = 7
    }
}