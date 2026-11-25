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
*文件名： IDrainableChannel
*版本号： v26.11.24.1
*唯一标识：3c1f0b7e-5a24-4d18-9e63-8b07a2d4c915
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/24 16:54:36
*描述：连接级排空规格：协议层声明关闭前还需读取的请求体字节上限与超时
*
*=====================================================================
*修改标记
*修改时间：2026/11/24 18:21:25
*修改人： yswenli
*版本号： v26.11.24.2
*描述：排空字节数改为长整型，避免大请求体剩余量被截断
*
*****************************************************************************/
namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 连接级排空规格：协议层在决定关闭连接前声明「还要等对端送多少字节、最多等多久」，
    /// 由执行器在收口末端取走并交给传输层执行半关与有界排空。
    /// <para>
    /// 规格只保留最近一次设置的有效值，取走即清空；未设置或取值非正时不排空，直接按普通关闭收口。
    /// </para>
    /// </summary>
    public interface IDrainableChannel
    {
        /// <summary>
        /// 设置排空规格。任一参数非正时忽略本次设置。
        /// </summary>
        /// <param name="drainBytes">关闭前还需读取的字节数，必须大于 0；超出该数量的字节不再等待。</param>
        /// <param name="timeoutMs">排空超时毫秒数，必须大于 0；超时后直接关闭。</param>
        void SetDrainSpec(long drainBytes, int timeoutMs);

        /// <summary>
        /// 取走排空规格；取走后再次调用返回 false，保证同一次收口只排空一次。
        /// </summary>
        /// <param name="drainBytes">关闭前还需读取的字节数；未设置时为 0。</param>
        /// <param name="timeoutMs">排空超时毫秒数；未设置时为 0。</param>
        /// <returns>存在有效规格时返回 true。</returns>
        bool TryTakeDrainSpec(out long drainBytes, out int timeoutMs);
    }
}