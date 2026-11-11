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
*命名空间：SAEA.P2P.Stun
*文件名： StunBindingFailure
*版本号： v26.11.5.1
*唯一标识：b48b601d-634f-4b1a-873b-8ed937856d52
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 17:20:00
*描述：Binding 请求失败原因
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 17:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 Binding 请求失败原因
*
*****************************************************************************/
namespace SAEA.P2P.Stun
{
    /// <summary>
    /// Binding 请求失败原因；调用方按原因决定换服务端、换凭据或退回中继
    /// </summary>
    public enum StunBindingFailure
    {
        /// <summary>
        /// 未失败
        /// </summary>
        None = 0,

        /// <summary>
        /// 事务超时，未收到任何应答
        /// </summary>
        Timeout = 1,

        /// <summary>
        /// 调用方取消
        /// </summary>
        Cancelled = 2,

        /// <summary>
        /// 传输层发送失败或套接字已不可用
        /// </summary>
        TransportError = 3,

        /// <summary>
        /// 应答报文无法解析或缺少必需属性
        /// </summary>
        ProtocolError = 4,

        /// <summary>
        /// 服务端返回 401 或 438，凭据缺失、错误或重试后仍被拒绝
        /// </summary>
        Unauthorized = 5,

        /// <summary>
        /// 服务端返回 5xx
        /// </summary>
        ServerError = 6
    }
}