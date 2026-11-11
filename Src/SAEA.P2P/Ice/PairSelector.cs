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
*命名空间：SAEA.P2P.Ice
*文件名： PairSelector
*版本号： v26.11.5.1
*唯一标识：1f6d8c2e-45b7-4a03-8f9d-c3e27b6a4d58
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 14:00:00
*描述：ICE 候选对选择与检查节奏控制
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 14:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 ICE 候选对选择器
*
*****************************************************************************/
using System;
using System.Collections.Generic;

namespace SAEA.P2P.Ice
{
    /// <summary>
    /// 候选对选择器：挑出下一个待检查的候选对，给出检查节奏，并维护优先级有序的检查表
    /// </summary>
    public sealed class PairSelector
    {
        private readonly int _taMilliseconds;

        /// <summary>
        /// 创建候选对选择器
        /// </summary>
        /// <param name="taMilliseconds">单个检查的基础间隔毫秒数，必须大于 0</param>
        /// <exception cref="ArgumentOutOfRangeException">基础间隔不是正整数毫秒</exception>
        public PairSelector(int taMilliseconds = 50)
        {
            if (taMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(taMilliseconds), "检查基础间隔必须是大于 0 的毫秒数");
            }
            _taMilliseconds = taMilliseconds;
        }

        /// <summary>
        /// 单个检查的基础间隔毫秒数
        /// </summary>
        public int TaMilliseconds
        {
            get { return _taMilliseconds; }
        }

        /// <summary>
        /// 在等待检查的候选对中挑出优先级最高的一个
        /// </summary>
        /// <param name="pairs">候选对集合，允许为空列表</param>
        /// <returns>选中的候选对；没有处于等待状态的候选对时返回 null</returns>
        /// <exception cref="ArgumentNullException">候选对集合为空</exception>
        public CandidatePair SelectNext(IReadOnlyList<CandidatePair> pairs)
        {
            if (pairs == null)
            {
                throw new ArgumentNullException(nameof(pairs));
            }

            CandidatePair best = null;
            for (int i = 0; i < pairs.Count; i++)
            {
                CandidatePair current = pairs[i];
                if (current == null || current.State != PairState.Waiting)
                {
                    continue;
                }
                // 状态由调用方迁移，这里只做挑选，不写回任何状态
                if (best == null || CompareForOrder(current, best) < 0)
                {
                    best = current;
                }
            }
            return best;
        }

        /// <summary>
        /// 按进行中的检查数放大检查节奏
        /// </summary>
        /// <param name="inProgressCount">当前处于检查中的候选对数量，小于 1 时按 1 计</param>
        /// <returns>本次检查前的等待间隔</returns>
        public TimeSpan GetNextCheckInterval(int inProgressCount)
        {
            // 进行中检查越多，节奏越慢，避免同一时刻集中放大流量
            int factor = Math.Max(1, inProgressCount);
            return TimeSpan.FromMilliseconds((double)_taMilliseconds * factor);
        }

        /// <summary>
        /// 按优先级降序原地排序候选对集合，优先级相同时按本端、远端端点字符串序
        /// </summary>
        /// <param name="pairs">待排序的候选对集合</param>
        /// <exception cref="ArgumentNullException">候选对集合为空</exception>
        public void SortByPriority(IList<CandidatePair> pairs)
        {
            if (pairs == null)
            {
                throw new ArgumentNullException(nameof(pairs));
            }

            // 插入排序：集合规模通常在数十量级，原地稳定排序即可，避免额外分配
            for (int i = 1; i < pairs.Count; i++)
            {
                CandidatePair current = pairs[i];
                int j = i - 1;
                while (j >= 0 && CompareForOrder(current, pairs[j]) < 0)
                {
                    pairs[j + 1] = pairs[j];
                    j--;
                }
                pairs[j + 1] = current;
            }
        }

        private static int CompareForOrder(CandidatePair left, CandidatePair right)
        {
            // 空元素排到末尾，避免排序阶段对空引用解引用
            if (left == null)
            {
                return right == null ? 0 : 1;
            }
            if (right == null)
            {
                return -1;
            }
            if (left.Priority != right.Priority)
            {
                return left.Priority > right.Priority ? -1 : 1;
            }

            // 优先级相同的候选对必须给出确定的先后次序，否则检查表顺序会随枚举顺序漂移
            int result = string.CompareOrdinal(left.Local.EndPoint.ToString(), right.Local.EndPoint.ToString());
            if (result != 0)
            {
                return result;
            }
            return string.CompareOrdinal(left.Remote.EndPoint.ToString(), right.Remote.EndPoint.ToString());
        }
    }
}