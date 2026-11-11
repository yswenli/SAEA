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
*命名空间：SAEA.P2P.Transport
*文件名： IUdpTransport
*版本号： v26.11.5.1
*唯一标识：29d95f0d-b054-4dc1-a770-3ec6a9f35841
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 12:10:00
*描述：UDP 传输契约
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 12:10:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 UDP 传输契约
*
*****************************************************************************/
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.P2P.Transport
{
    /// <summary>
    /// 无连接报文传输契约：绑定一个本地端点后，按数据报与任意远端端点收发。
    /// 实现不调用 UDP 的 connect，来源地址随每个数据报给出。
    /// </summary>
    public interface IUdpTransport : ITransport
    {
        /// <summary>
        /// 收到数据报时触发，参数为来源端点与已经复制好的数据；回调返回后两者仍可继续使用
        /// </summary>
        event Action<IPEndPoint, ReadOnlyMemory<byte>> DatagramReceived;

        /// <summary>
        /// 绑定本地端点；重复绑定或已释放时抛异常
        /// </summary>
        /// <param name="localEndPoint">本地端点，端口为 0 时由系统分配</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>绑定完成的任务</returns>
        Task BindAsync(IPEndPoint localEndPoint, CancellationToken cancellationToken);

        /// <summary>
        /// 启动接收循环，幂等；底层实现若在绑定时已开始接收，则本方法为空操作
        /// </summary>
        void StartReceive();

        /// <summary>
        /// 向任意远端端点发送一个数据报，目标端口按原样使用
        /// </summary>
        /// <param name="remoteEndPoint">目标端点</param>
        /// <param name="data">待发送数据，长度不得超过 UDP 单包上限</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>发送完成的任务</returns>
        Task SendToAsync(IPEndPoint remoteEndPoint, ReadOnlyMemory<byte> data, CancellationToken cancellationToken);
    }
}