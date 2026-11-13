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
*命名空间：SAEA.P2P.Core
*文件名： P2PDataReceivedHandler
*版本号： v26.11.5.1
*唯一标识：13851ac6-6a25-4db5-82c3-5993d588bb96
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 10:56:15
*描述：业务帧接收委托
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 10:56:15
*修改人： yswenli
*版本号： v26.11.5.1
*描述：业务帧接收委托
*
*****************************************************************************/
using System;

namespace SAEA.P2P.Core
{
    // ReadOnlySpan<byte> 是 ref struct，不能作泛型实参，因此这里必须用自定义委托而不是 Action<...>
    /// <summary>
    /// 业务帧接收委托：一次回调对应一条完整业务帧，<paramref name="data"/> 只在回调期间有效
    /// </summary>
    /// <param name="peerId">来源对端节点标识</param>
    /// <param name="data">业务载荷，不含数据面前缀；跨回调保存必须复制</param>
    public delegate void P2PDataReceivedHandler(string peerId, ReadOnlySpan<byte> data);
}
