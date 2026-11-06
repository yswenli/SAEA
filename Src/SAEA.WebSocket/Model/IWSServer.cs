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
*修改时间：2026/11/6 12:52:30
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Generic;

namespace SAEA.WebSocket.Model
{
    /// <summary>
    /// WebSocket 服务端定义，暴露连接、消息事件与主动收发能力
    /// </summary>
    public interface IWSServer
    {
        /// <summary>
        /// 客户端连接成功事件，参数为客户端会话 ID
        /// </summary>
        event Action<string> OnConnected;

        /// <summary>
        /// 客户端消息事件，参数为客户端会话 ID 与收到的协议帧
        /// </summary>
        event Action<string, WSProtocal> OnMessage;

        /// <summary>
        /// 客户端断开连接事件，参数为客户端会话 ID
        /// </summary>
        event Action<string> OnDisconnected;

        /// <summary>
        /// 已连接的客户端会话 ID 列表
        /// </summary>
        List<string> Clients {  set; get; }

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