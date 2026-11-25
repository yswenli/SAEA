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
*文件名： IDrainedCloseStream
*版本号： v26.11.24.1
*唯一标识：a6d90f43-71c8-4b52-8f3e-2d4c5b6a7e08
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/24 16:54:36
*描述：支持半关后按字节上限与超时排空再关闭的字节流接入面
*
*=====================================================================
*修改标记
*修改时间：2026/11/24 18:21:25
*修改人： yswenli
*版本号： v26.11.24.2
*描述：排空字节数改为长整型，避免大请求体剩余量被截断
*
*****************************************************************************/
using System.Threading.Tasks;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 支持「先半关、再有界排空、最后关闭」的字节流接入面。
    /// <para>
    /// 实现方先关闭发送方向，让对端立即看到响应与结束信号，然后继续读取最多
    /// <paramref name="drainBytes"/> 字节或等待 <paramref name="timeoutMs"/> 超时，再释放连接。
    /// 返回的任务在连接真正关闭后才完成。
    /// </para>
    /// </summary>
    public interface IDrainedCloseStream
    {
        /// <summary>
        /// 半关发送方向并按规格排空入站字节后关闭连接。
        /// </summary>
        /// <param name="reason">关闭原因，用于日志与传输层区分收口来源。</param>
        /// <param name="drainBytes">关闭前还需读取的字节数，必须大于 0。</param>
        /// <param name="timeoutMs">排空超时毫秒数，必须大于 0。</param>
        /// <returns>连接真正关闭后完成的任务。</returns>
        Task DrainAndCloseAsync(string reason, long drainBytes, int timeoutMs);
    }
}