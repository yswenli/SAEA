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
*命名空间：SAEA.WebSocket.Model
*文件名： IWSServer
*版本号： v26.4.23.1
*唯一标识：ca58f599-d0c0-4bf0-b39c-bc6d408dc45b
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/06/14 11:19:42
*描述：IWSServer接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 13:52:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：删除面向使用者的接收与生命周期事件，改由 ConfigurePipeline 装配处理器链
*
*****************************************************************************/
using System.Collections.Generic;
using SAEA.Sockets.Pipeline;

namespace SAEA.WebSocket.Model
{
    /// <summary>
    /// WebSocket 服务端定义，暴露处理器链装配入口、会话列表与主动收发能力
    /// </summary>
    public interface IWSServer
    {
        /// <summary>
        /// 处理器链装配回调：每条连接在帧解码器之后、异常收口之前调用一次，
        /// 接收会话 ID 与尚未启动的链；为 null 时只保留内置处理器
        /// </summary>
        System.Action<string, IChannelPipeline> ConfigurePipeline { get; set; }

        /// <summary>
        /// 当前已建立的会话 ID 列表快照；连接建立即登记，收口时移除
        /// </summary>
        List<string> Clients { get; }

        /// <summary>
        /// 启动监听
        /// </summary>
        /// <param name="backlog">挂起连接队列的最大长度，默认 10000</param>
        void Start(int backlog = 10 * 1000);


        /// <summary>
        /// 向指定客户端回复消息
        /// </summary>
        /// <param name="id">客户端会话 ID</param>
        /// <param name="data">待发送的协议帧</param>
        void Reply(string id, WSProtocal data);


        /// <summary>
        /// 向指定客户端发送关闭帧后断开连接
        /// </summary>
        /// <param name="id">客户端会话 ID</param>
        /// <param name="data">关闭帧内容</param>
        void Disconnect(string id, WSProtocal data);


        /// <summary>
        /// 直接断开指定客户端
        /// </summary>
        /// <param name="id">客户端会话 ID</param>
        void Disconnect(string id);

        /// <summary>
        /// 停止服务器
        /// </summary>
        void Stop();
    }
}