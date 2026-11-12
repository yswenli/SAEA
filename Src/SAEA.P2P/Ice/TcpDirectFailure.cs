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
*命名空间：SAEA.P2P.Ice
*文件名： TcpDirectFailure
*版本号： v26.11.5.1
*唯一标识：903a92dc-f99b-4fb8-bc1d-a16a63e9dd0d
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 13:33:14
*描述：TCP 直连检查失败原因
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 13:33:14
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TCP 直连检查失败原因
*
*****************************************************************************/
namespace SAEA.P2P.Ice
{
    /// <summary>
    /// TCP 直连通道检查的失败原因，检查通过时取 <see cref="None"/>
    /// </summary>
    public enum TcpDirectFailure
    {
        /// <summary>
        /// 没有失败
        /// </summary>
        None = 0,

        /// <summary>
        /// 连接（含本地绑定）在重试后仍失败
        /// </summary>
        ConnectFailed = 1,

        /// <summary>
        /// 首个响应帧不是 STUN 消息
        /// </summary>
        NonStunFrame = 2,

        /// <summary>
        /// MESSAGE-INTEGRITY 校验失败
        /// </summary>
        IntegrityFailed = 3,

        /// <summary>
        /// 收到错误应答，错误码见结果
        /// </summary>
        Rejected = 4,

        /// <summary>
        /// 检查超时未收到应答
        /// </summary>
        Timeout = 5,

        /// <summary>
        /// 收发异常或对端提前关闭
        /// </summary>
        TransportError = 6,

        /// <summary>
        /// 调用方取消
        /// </summary>
        Cancelled = 7
    }
}