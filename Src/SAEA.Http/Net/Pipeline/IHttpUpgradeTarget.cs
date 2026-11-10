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
 *命名空间：SAEA.Http
 *文件名： IHttpUpgradeTarget
 *版本号： v26.11.5.1
 *唯一标识：d6cbb327-2869-4807-b106-0b2608ed3321
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/9 13:19:30
 *描述：HTTP 协议升级的目标：由业务决定是否接受升级并返回握手响应
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/9 13:19:30
 *修改人： yswenli
 *版本号： v26.11.5.1
 *描述：HTTP 协议升级的目标：由业务决定是否接受升级并返回握手响应
 *
 *****************************************************************************/
using System.Threading.Tasks;
using SAEA.Http.Base;
using SAEA.Sockets.Pipeline;

namespace SAEA.Http
{
    /// <summary>
    /// HTTP 协议升级目标：分帧器在切出 <c>Upgrade: websocket</c> 请求后停切，
    /// 由实现方决定是否接受升级，并给出要写回的握手响应。
    /// </summary>
    public interface IHttpUpgradeTarget
    {
        /// <summary>判断并完成一次升级握手；返回 null 表示拒绝升级。</summary>
        /// <param name="ctx">当前协议处理器的节点上下文，供实现方读取连接信息或发起业务写。</param>
        /// <param name="request">触发本次升级的完整请求。</param>
        /// <returns>接受升级时返回握手响应（通常为 101）；拒绝时返回 null。</returns>
        Task<HttpResponse> TryUpgradeAsync(IChannelHandlerContext ctx, HttpMessage request);
    }
}