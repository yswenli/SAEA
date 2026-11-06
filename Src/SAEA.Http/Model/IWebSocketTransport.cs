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
*命名空间：SAEA.Http.Model
*文件名： IWebSocketTransport
*版本号： v26.11.5.1
*唯一标识：212993eb-d0e2-4c66-bdc2-b070e89eb418
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/5 11:56:30
*描述：已升级 WebSocket 连接的底层传输抽象
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 13:11:19
*修改人： yswenli
*版本号： v26.11.5.1
*描述：规范文件头 banner
*
*****************************************************************************/
namespace SAEA.Http.Model
{
    /// <summary>
    /// 一条已经升级为 WebSocket 的底层传输通道。
    /// 由 HttpResponse.Upgrade 返回，上层只依赖本接口，不需要接触 IUserToken / IWebHost 等内部对象。
    /// </summary>
    public interface IWebSocketTransport
    {
        /// <summary>
        /// 底层会话标识
        /// </summary>
        string SessionId { get; }

        /// <summary>
        /// 底层 socket 是否仍然存在。会话被回收（或已主动断开）后返回 false，
        /// 用于兜底清理错过断开通知的连接
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 发送原始字节（服务端方向，不做掩码）
        /// </summary>
        /// <param name="data">要发送的字节</param>
        void Send(byte[] data);

        /// <summary>
        /// 设置/取消空闲超时豁免。长连接期间必须豁免，否则会被会话清理定时器断开
        /// </summary>
        /// <param name="noTimeout">true 表示不参与空闲清理</param>
        void SetNoTimeout(bool noTimeout);

        /// <summary>
        /// 等待最后一次发送完成（用于关闭前确保 Close 帧已写出）。
        /// 发送超时或底层已断开时返回 false
        /// </summary>
        /// <param name="timeoutMs">最长等待毫秒数</param>
        /// <returns>最后一次发送已完成返回 true，超时或连接已断开返回 false</returns>
        bool WaitSendCompleted(int timeoutMs);

        /// <summary>
        /// 关闭发送方向（半关闭）。先发 FIN 再 Close，避免内核直接 reset 把已经写出的 Close 帧丢掉
        /// </summary>
        void ShutdownSend();

        /// <summary>
        /// 断开底层连接（幂等）
        /// </summary>
        void Disconnect();
    }
}