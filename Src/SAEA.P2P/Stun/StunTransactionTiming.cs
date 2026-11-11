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
*命名空间：SAEA.P2P.Stun
*文件名： StunTransactionTiming
*版本号： v26.11.5.1
*唯一标识：d94b948b-bb4a-472b-8e3f-5ba160859f54
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 16:20:00
*描述：STUN 事务重传时刻表
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 16:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 STUN 事务重传时刻表
*
*****************************************************************************/
using System;

namespace SAEA.P2P.Stun
{
    /// <summary>
    /// STUN 事务计时参数：Rc=7 表示总共发送 7 次，第 k 次时刻为 RTO×(2^k−1)；
    /// 最后一次发送后还要再等 Rm 倍 RTO 才判失败。
    /// </summary>
    public static class StunTransactionTiming
    {
        /// <summary>
        /// 初始重传间隔，单位毫秒
        /// </summary>
        public const int RtoMilliseconds = 500;

        /// <summary>
        /// 最后一次发送后的额外等待倍数
        /// </summary>
        public const int Rm = 16;

        /// <summary>
        /// 总发送次数，含首发
        /// </summary>
        public const int TotalSendCount = 7;

        /// <summary>
        /// 计算第 k 次发送相对事务开始的偏移；k 从 0 开始，0 为首发时刻
        /// </summary>
        /// <param name="sendIndex">发送序号，取值 0 到 6</param>
        /// <param name="rtoMilliseconds">初始重传间隔，单位毫秒</param>
        /// <returns>偏移毫秒数</returns>
        public static int GetSendOffsetMilliseconds(int sendIndex, int rtoMilliseconds)
        {
            if (sendIndex < 0 || sendIndex >= TotalSendCount)
            {
                throw new ArgumentOutOfRangeException(nameof(sendIndex));
            }
            if (rtoMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rtoMilliseconds));
            }

            return rtoMilliseconds * ((1 << sendIndex) - 1);
        }

        /// <summary>
        /// 计算事务总超时：最后一次发送偏移加上 Rm 倍 RTO；默认参数下为 39500ms
        /// </summary>
        /// <param name="rtoMilliseconds">初始重传间隔，单位毫秒</param>
        /// <returns>超时毫秒数</returns>
        public static int GetTimeoutMilliseconds(int rtoMilliseconds)
        {
            return GetSendOffsetMilliseconds(TotalSendCount - 1, rtoMilliseconds) + (Rm * rtoMilliseconds);
        }
    }
}