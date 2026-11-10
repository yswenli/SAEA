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
*文件名： WsHandshakeState
*版本号： v26.11.5.1
*唯一标识：1da9b7c8-1460-4b7f-94a2-9cb1296b221d
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 13:50:00
*描述：单条 WebSocket 连接的握手完成状态
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 13:50:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：单条 WebSocket 连接的握手完成状态
*
*****************************************************************************/
using System.Threading;

namespace SAEA.WebSocket
{
    /// <summary>
    /// 单条连接的握手完成标记：握手处理器置位，出站编码器据此拒绝握手完成前的数据帧。
    /// <para>
    /// 握手应答与请求本身是原始帧，不经过该标记；数据帧一旦早于握手发出，对端会按协议错误断开，
    /// 因此在源头上失败比让脏帧上线更安全。实例只服务一条连接，不跨连接复用。
    /// </para>
    /// </summary>
    public sealed class WsHandshakeState
    {
        private int _completed;

        /// <summary>握手是否已经成功完成。</summary>
        public bool IsCompleted
        {
            get { return Volatile.Read(ref _completed) != 0; }
        }

        /// <summary>标记握手完成；重复调用无副作用。</summary>
        public void MarkCompleted()
        {
            Interlocked.Exchange(ref _completed, 1);
        }
    }
}