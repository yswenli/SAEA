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
*命名空间：SAEA.Sockets.Pipeline
*文件名： ChannelTraffic
*版本号： v26.11.5.1
*唯一标识：24226833-d116-4840-bc2e-b2fe45ef7020
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：连接级流量的只读快照
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：连接级流量的只读快照
*
*****************************************************************************/
namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 连接级流量的只读快照。三个值各自原子读取，不保证严格同步，
    /// 仅用于防护判断，不作为精确账本。
    /// </summary>
    public readonly struct ChannelTraffic
    {
        /// <summary>本连接累计接收的字节数，只增不减。</summary>
        public long TotalReceivedBytes { get; }

        /// <summary>当前已缓冲但尚未成帧的残余字节数。</summary>
        public long BufferedBytes { get; }

        /// <summary>当前队列中的消息数。</summary>
        public int QueuedMessages { get; }

        /// <summary>创建一份流量快照。</summary>
        public ChannelTraffic(long totalReceivedBytes, long bufferedBytes, int queuedMessages)
        {
            TotalReceivedBytes = totalReceivedBytes;
            BufferedBytes = bufferedBytes;
            QueuedMessages = queuedMessages;
        }
    }
}