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
*命名空间：SAEA.RPC.Net
*文件名： RSocketMsg
*版本号： v26.4.23.1
*唯一标识：5999d7de-2387-4e5e-bd79-85a959ceae59
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/05/25 17:28:26
*描述：RSocketMsg类
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 15:20:36
*修改人： yswenli
*版本号： v26.11.5.1
*描述：Dispose 不再清零 Data，避免改写已交给业务线程或下游处理器的数据
*
*****************************************************************************/
using SAEA.RPC.Model;
using System;

namespace SAEA.RPC.Net
{
    /// <summary>
    /// RPC数据传输实体
    /// </summary>
    public class RSocketMsg : IDisposable
    {

        const int MIN = (1 + 4 + 8 + 4 + 0 + 4 + 0 + 0);

        /// <summary>
        /// 消息类型，取值来自 RSocketMsgType
        /// </summary>
        public byte Type
        {
            get; set;
        }

        /// <summary>
        /// 整帧总长度，不含类型字节
        /// </summary>
        public uint Total
        {
            get; set;
        }

        /// <summary>
        /// 请求序号，用于匹配请求与响应
        /// </summary>
        public long SequenceNumber
        {
            get; set;
        }

        /// <summary>
        /// 服务名字节长度
        /// </summary>
        public uint SLen
        {
            get; set;
        }

        /// <summary>
        /// 服务名
        /// </summary>
        public string ServiceName
        {
            get; set;
        }

        /// <summary>
        /// 方法名字节长度
        /// </summary>
        public uint MLen
        {
            get; set;
        }

        /// <summary>
        /// 方法名
        /// </summary>
        public string MethodName
        {
            get; set;
        }

        /// <summary>
        /// 序列化后的调用参数或返回值
        /// </summary>
        public byte[] Data
        {
            get; set;
        }

        /// <summary>
        /// 数据是否来自内存池
        /// </summary>
        public bool IsPooled { get; set; }

        /// <summary>
        /// 以指定类型创建消息
        /// </summary>
        /// <param name="type">消息类型</param>
        public RSocketMsg(RSocketMsgType type) : this(type, string.Empty)
        {

        }
        /// <summary>
        /// 以指定类型和服务名创建消息
        /// </summary>
        /// <param name="type">消息类型</param>
        /// <param name="serviceName">服务名</param>
        public RSocketMsg(RSocketMsgType type, string serviceName) : this(type, serviceName, string.Empty)
        {

        }
        /// <summary>
        /// 以指定类型、服务名和方法名创建消息
        /// </summary>
        /// <param name="type">消息类型</param>
        /// <param name="serviceName">服务名</param>
        /// <param name="method">方法名</param>
        public RSocketMsg(RSocketMsgType type, string serviceName, string method) : this(type, serviceName, method, null)
        {

        }
        /// <summary>
        /// 以指定类型、服务名、方法名和数据创建消息
        /// </summary>
        /// <param name="type">消息类型</param>
        /// <param name="serviceName">服务名</param>
        /// <param name="method">方法名</param>
        /// <param name="data">序列化后的数据</param>
        public RSocketMsg(RSocketMsgType type, string serviceName, string method, byte[] data)
        {
            this.Type = (byte)type;
            this.ServiceName = serviceName;
            this.MethodName = method;
            this.Data = data;
        }

        /// <summary>
        /// 清空消息内容并解除对数据数组的引用
        /// </summary>
        public void Dispose()
        {
            this.ServiceName = this.MethodName =  string.Empty;
            // 不在这里清空 Data：数据数组的所有权可能已经交给业务线程或下游处理器，
            // 清空会改写它们仍在使用的内容；消息对象本身不被池化，清空没有收益。
            this.Data = null;
            this.Total = this.SLen = this.MLen;
            this.Type = 0;
        }
    }
}