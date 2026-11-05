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
 *命名空间：SAEA.Http.Model
 *文件名： IWebSocketConnection
 *描述：已建立的 WebSocket 连接契约与状态枚举
 *
 *****************************************************************************/
using System.Threading.Tasks;

namespace SAEA.Http.Model
{
    /// <summary>
    /// WebSocket 连接状态
    /// </summary>
    public enum WebSocketState
    {
        /// <summary>
        /// 已打开，可以收发数据
        /// </summary>
        Open = 1,

        /// <summary>
        /// 正在关闭（已发出或正在发送 Close 帧，等待收尾完成）
        /// </summary>
        Closing = 2,

        /// <summary>
        /// 已关闭
        /// </summary>
        Closed = 3
    }

    /// <summary>
    /// WebSocket 关闭原因，映射到 RFC 6455 的关闭状态码
    /// </summary>
    public enum WSCloseReason
    {
        /// <summary>
        /// 正常关闭（1000）
        /// </summary>
        Normal = 1000,

        /// <summary>
        /// 端点离开（1001）
        /// </summary>
        GoingAway = 1001,

        /// <summary>
        /// 协议错误（1002）
        /// </summary>
        ProtocolError = 1002,

        /// <summary>
        /// 服务端关闭（1011）
        /// </summary>
        ServerShutdown = 1011,

        /// <summary>
        /// 异常断开（1006）。按 RFC 6455 不允许出现在 Close 帧中，只用于 CloseAsync 回调的入参
        /// </summary>
        Abnormal = 1006
    }

    /// <summary>
    /// 已建立的 WebSocket 连接
    /// </summary>
    public interface IWebSocketConnection
    {
        /// <summary>
        /// 会话标识（底层 socket 会话 id）
        /// </summary>
        string Id { get; }

        /// <summary>
        /// 当前连接状态
        /// </summary>
        WebSocketState State { get; }

        /// <summary>
        /// 发送文本消息
        /// </summary>
        /// <param name="text">文本内容，UTF-8 编码后发送</param>
        Task SendAsync(string text);

        /// <summary>
        /// 发送二进制消息
        /// </summary>
        /// <param name="data">二进制内容</param>
        Task SendAsync(byte[] data);

        /// <summary>
        /// 关闭连接：发送 Close 帧后断开
        /// </summary>
        /// <param name="reason">关闭原因</param>
        Task CloseAsync(WSCloseReason reason = WSCloseReason.Normal);

        /// <summary>
        /// 立即中止连接，不发送 Close 帧
        /// </summary>
        void Abort();
    }
}