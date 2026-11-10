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
*命名空间：SAEA.WebSocket
*文件名： WebSocketProtocolErrors
*版本号： v26.11.5.1
*唯一标识：f8201d20-2af5-4cc1-9842-bd946b5b4f3c
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 13:04:00
*描述：WebSocket 分帧与握手阶段的协议错误 token
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 14:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：删除不再使用的 UnexpectedHandshakeFrame 与 HandshakeTimeout token
*
*****************************************************************************/
namespace SAEA.WebSocket
{
    /// <summary>
    /// WebSocket 分帧器与握手处理器的协议错误 token。
    /// <para>
    /// <see cref="SAEA.Sockets.Pipeline.FrameProtocolException"/> 是 sealed 类型，无法用派生类承载错误种类，
    /// 因此把固定 token 直接写进异常 <c>Message</c>，链上的处理器按 token 决定写 400 还是直接断开。
    /// </para>
    /// </summary>
    public static class WebSocketProtocolErrors
    {
        /// <summary>握手报头在结束符到齐前就超过上限。</summary>
        public const string HandshakeHeaderTooLarge = "ws-handshake-header-too-large";

        /// <summary>握手请求缺少请求行、Upgrade 或 Sec-WebSocket-Key。</summary>
        public const string InvalidHandshakeRequest = "ws-handshake-request";

        /// <summary>握手应答不是 101 或缺少 Sec-WebSocket-Accept。</summary>
        public const string InvalidHandshakeResponse = "ws-handshake-response";

        /// <summary>wire frame 不足最小头长度，与拆帧器产出不一致。</summary>
        public const string FrameTooShort = "ws-frame-too-short";

        /// <summary>帧头出现未协商的 RSV 位。</summary>
        public const string UnsupportedRsv = "ws-rsv";

        /// <summary>出现不支持的 opcode。</summary>
        public const string UnsupportedOpcode = "ws-opcode";

        /// <summary>服务端方向收到未带掩码的帧。</summary>
        public const string MissingMask = "ws-mask-required";

        /// <summary>帧长度字段非法或超出可索引上限。</summary>
        public const string InvalidLength = "ws-length";

        /// <summary>控制帧被分片。</summary>
        public const string ControlFrameFragmented = "ws-control-fragmented";

        /// <summary>控制帧帧体超过 125 字节。</summary>
        public const string ControlFrameTooLarge = "ws-control-too-large";

        /// <summary>单帧或分片重组后的消息超过 MaxMessageSize。</summary>
        public const string MessageTooLarge = "ws-message-too-large";

        /// <summary>收到没有起始数据帧的 Continuation 帧。</summary>
        public const string UnexpectedContinuation = "ws-unexpected-continuation";

        /// <summary>上一条分片消息未结束时又收到新的数据帧。</summary>
        public const string InterleavedDataFrame = "ws-interleaved-data-frame";

        /// <summary>帧头声明的长度与拆帧器给出的实际字节数不一致。</summary>
        public const string FrameLengthMismatch = "ws-frame-length-mismatch";
    }
}