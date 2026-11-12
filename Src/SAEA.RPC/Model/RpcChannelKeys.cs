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
*命名空间：SAEA.RPC
*文件名： RpcChannelKeys
*版本号： v26.11.5.1
*唯一标识：77473d8e-7975-4945-9405-6c5cb19ee36c
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 16:45:50
*描述：RPC 写入连接通道的属性键
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 16:45:50
*修改人： yswenli
*版本号： v26.11.5.1
*描述：RPC 写入连接通道的属性键
*
*****************************************************************************/
namespace SAEA.RPC
{
    /// <summary>
    /// RPC 写入连接通道的属性键。
    /// </summary>
    public static class RpcChannelKeys
    {
        /// <summary>
        /// 客户端地址。过滤器通过 channel.GetAttribute(RpcChannelKeys.RemoteEndPoint) 取客户端地址，
        /// 值为 <see cref="System.Net.EndPoint"/>，连接建立后写入、随连接关闭清空。
        /// </summary>
        public const string RemoteEndPoint = "remoteEndPoint";
    }
}