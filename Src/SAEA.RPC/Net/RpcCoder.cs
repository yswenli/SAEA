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
*命名空间：SAEA.RPC.Net
*文件名： RpcCoder
*版本号： v26.4.23.1
*唯一标识：f43a2f86-586f-4a36-9882-a68e706fde41
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2025/02/10 17:07:21
*描述：RpcCoder编解码类
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 16:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：解码与分帧改由处理器链承担，本类只保留 Encode 并委托 RpcMessageEncoder
*
*****************************************************************************/
namespace SAEA.RPC.Net
{
    /// <summary>
    /// SAEA.RPC传输编解码
    /// 格式为：1+4+8+4+x+4+x+x
    /// </summary>
    public sealed class RpcCoder
    {
        /// <summary>
        /// socket 传输字节编码
        /// 格式为：1+4+8+4+x+4+x+x
        /// </summary>
        /// <param name="queueSocketMsg">待编码的消息</param>
        /// <returns>完整的 RPC 线格式字节</returns>
        public byte[] Encode(RSocketMsg queueSocketMsg)
        {
            return RpcMessageEncoder.Encode(queueSocketMsg);
        }
    }
}