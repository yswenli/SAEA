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
*命名空间：SAEA.Sockets
*文件名： IGracefulCloseFlow
*版本号： v26.11.24.1
*唯一标识：f2b45d18-9e37-4c60-a1d8-7b53e9c204a6
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/24 16:55:00
*描述：流控制闸门上的半关排空关闭入口
*
*=====================================================================
*修改标记
*修改时间：2026/11/24 18:12:35
*修改人： yswenli
*版本号： 
*描述：v26.11.24.2
*
*****************************************************************************/
using System.Threading.Tasks;

namespace SAEA.Sockets
{
    /// <summary>
    /// 接收流量控制闸门上的半关排空入口：先半关发送方向，再读取剩余入站字节，最后关闭连接。
    /// </summary>
    public interface IGracefulCloseFlow
    {
        /// <summary>
        /// 半关发送方向并按规格排空入站字节后关闭连接。
        /// </summary>
        /// <param name="drainBytes">关闭前还需读取的字节数，必须大于 0。</param>
        /// <param name="timeoutMs">排空超时毫秒数，必须大于 0。</param>
        /// <returns>连接真正关闭后完成的任务。</returns>
        Task DrainAndCloseAsync(long drainBytes, int timeoutMs);
    }
}