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
*文件名： CheckList
*版本号： v26.11.5.1
*唯一标识：19a8711c-214b-4730-a001-4612df0cd270
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 14:30:00
*描述：ICE 检查表与增量配对
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 21:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：配对地址族比较归一化 v4-mapped
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using SAEA.P2P.Common;

namespace SAEA.P2P.Ice
{
    /// <summary>
    /// ICE 检查表：增量配对本地与远端候选，维护按优先级排序的候选对、解冻顺序与触发检查队列
    /// </summary>
    public sealed class CheckList
    {
        private readonly List<CandidatePair> _pairs = new List<CandidatePair>();
        private readonly List<Candidate> _localCandidates = new List<Candidate>();
        private readonly List<Candidate> _remoteCandidates = new List<Candidate>();
        private readonly List<CandidatePair> _triggeredPairs = new List<CandidatePair>();
        private readonly PairSelector _selector = new PairSelector();
        private bool _isNominationCompleted;

        /// <summary>
        /// 创建检查表
        /// </summary>
        /// <param name="isControlling">本地是否为控制方，决定候选对优先级里 G/D 的取值方向</param>
        public CheckList(bool isControlling)
        {
            IsControlling = isControlling;
        }

        /// <summary>
        /// 本地是否为控制方，角色切换后由 <see cref="UpdateRole"/> 更新
        /// </summary>
        public bool IsControlling { get; private set; }

        /// <summary>
        /// 提名是否已完成，完成后不再接受新的远端候选
        /// </summary>
        public bool IsNominationCompleted
        {
            get { return _isNominationCompleted; }
        }

        /// <summary>
        /// 候选对数量
        /// </summary>
        public int PairCount
        {
            get { return _pairs.Count; }
        }

        /// <summary>
        /// 按候选对优先级降序排列的候选对快照
        /// </summary>
        public IReadOnlyList<CandidatePair> Pairs
        {
            // 返回新数组，外部拿到后改动不了内部列表，也不会破坏内部排序
            get { return _pairs.ToArray(); }
        }

        /// <summary>
        /// 添加本地候选，并与所有已存在的远端候选配对
        /// </summary>
        /// <param name="local">本地候选</param>
        /// <exception cref="ArgumentNullException">本地候选为空</exception>
        public void AddLocalCandidate(Candidate local)
        {
            if (local == null)
            {
                throw new ArgumentNullException(nameof(local));
            }

            _localCandidates.Add(local);

            // 只追加新候选对，已有候选对实例与状态保持不变
            List<string> affectedGroups = new List<string>();
            for (int i = 0; i < _remoteCandidates.Count; i++)
            {
                AddPair(local, _remoteCandidates[i], affectedGroups);
            }
            CommitAddedPairs(affectedGroups);
        }

        /// <summary>
        /// 添加远端候选，并与所有已存在的本地候选配对；提名完成后忽略该候选
        /// </summary>
        /// <param name="remote">远端候选</param>
        /// <exception cref="ArgumentNullException">远端候选为空</exception>
        public void AddRemoteCandidate(Candidate remote)
        {
            if (remote == null)
            {
                throw new ArgumentNullException(nameof(remote));
            }

            // 提名完成后再引入远端候选会带出未参与提名的候选对，只能整体忽略
            if (_isNominationCompleted)
            {
                P2PLogHelper.Debug(nameof(CheckList), string.Format("提名已完成，忽略远端候选 {0}", remote.EndPoint));
                return;
            }

            _remoteCandidates.Add(remote);

            List<string> affectedGroups = new List<string>();
            for (int i = 0; i < _localCandidates.Count; i++)
            {
                AddPair(_localCandidates[i], remote, affectedGroups);
            }
            CommitAddedPairs(affectedGroups);
        }

        /// <summary>
        /// 标记提名阶段结束，重复调用不产生副作用
        /// </summary>
        public void MarkNominationCompleted()
        {
            _isNominationCompleted = true;
        }

        /// <summary>
        /// 更新本地检查角色，并按新角色重算全部候选对优先级
        /// </summary>
        /// <param name="isControlling">本地是否为控制方</param>
        public void UpdateRole(bool isControlling)
        {
            IsControlling = isControlling;

            // 角色翻转后 G/D 的归属互换，逐个候选对重算后再重排，顺序才能与新角色一致
            for (int i = 0; i < _pairs.Count; i++)
            {
                _pairs[i].RecomputePriority(isControlling);
            }
            _selector.SortByPriority(_pairs);
        }

        /// <summary>
        /// 选出下一个待检查的候选对并置为检查中
        /// </summary>
        /// <returns>选中的候选对；没有等待检查的候选对时返回 null</returns>
        public CandidatePair SelectNext()
        {
            CandidatePair pair = _selector.SelectNext(_pairs);
            if (pair == null)
            {
                return null;
            }

            // 立刻进入检查中，避免同一轮调度里被重复挑出
            pair.MarkInProgress();
            return pair;
        }

        /// <summary>
        /// 取触发检查队列里第一个仍处于等待的候选对
        /// </summary>
        /// <returns>出队的候选对；队列里没有可检查的候选对时返回 null</returns>
        public CandidatePair SelectTriggered()
        {
            while (_triggeredPairs.Count > 0)
            {
                CandidatePair pair = _triggeredPairs[0];
                _triggeredPairs.RemoveAt(0);

                if (pair.State == PairState.Waiting)
                {
                    return pair;
                }
            }
            return null;
        }

        /// <summary>
        /// 把候选对放入触发检查队列，并恢复为可检查状态
        /// </summary>
        /// <param name="pair">检查表内的候选对</param>
        /// <exception cref="ArgumentNullException">候选对为空</exception>
        /// <exception cref="ArgumentException">候选对不在检查表内</exception>
        public void TriggerPair(CandidatePair pair)
        {
            if (pair == null)
            {
                throw new ArgumentNullException(nameof(pair));
            }
            if (!_pairs.Contains(pair))
            {
                throw new ArgumentException("候选对不在检查表内", nameof(pair));
            }

            // 同一候选对重复触发只保留一个队列项，避免重复发送检查请求
            if (!_triggeredPairs.Contains(pair))
            {
                _triggeredPairs.Add(pair);
            }

            // 等待中的候选对保持原状，失败与冻结的需要回到等待才能被再次选中
            if (pair.State == PairState.Failed)
            {
                pair.ResetToWaiting();
            }
            else if (pair.State == PairState.Frozen)
            {
                pair.MarkWaiting();
            }
        }

        /// <summary>
        /// 按进行中的检查数取下一次检查前的等待间隔
        /// </summary>
        /// <param name="inProgressCount">处于检查中的候选对数量，小于 1 时按 1 计</param>
        /// <returns>本次检查前的等待间隔</returns>
        public TimeSpan GetNextCheckInterval(int inProgressCount)
        {
            return _selector.GetNextCheckInterval(inProgressCount);
        }

        /// <summary>
        /// 通知候选对检查失败，并在同组内解冻下一个候选对
        /// </summary>
        /// <param name="pair">检查失败的候选对</param>
        /// <exception cref="ArgumentNullException">候选对为空</exception>
        public void NotifyPairFailed(CandidatePair pair)
        {
            if (pair == null)
            {
                throw new ArgumentNullException(nameof(pair));
            }

            // 已成功的候选对不允许回退，重复报告失败按幂等处理
            if (pair.State != PairState.Failed && pair.State != PairState.Succeeded)
            {
                pair.MarkFailed();
            }

            UnfreezeGroup(GetGroupKey(pair));
        }

        private void AddPair(Candidate local, Candidate remote, List<string> affectedGroups)
        {
            if (!CanPair(local, remote))
            {
                return;
            }
            if (IsDuplicate(local, remote))
            {
                return;
            }

            _pairs.Add(new CandidatePair(local, remote, IsControlling));
            affectedGroups.Add(GetGroupKey(local, remote));
        }

        private void CommitAddedPairs(List<string> affectedGroups)
        {
            _selector.SortByPriority(_pairs);

            // 只为新增候选对涉及的组尝试解冻，其他组的解冻状态不受本次添加影响
            for (int i = 0; i < affectedGroups.Count; i++)
            {
                UnfreezeGroup(affectedGroups[i]);
            }
        }

        private static bool CanPair(Candidate local, Candidate remote)
        {
            // 配对阶段同样归一化，避免对端候选携带 v4-mapped 时被误判为跨族
            if (GetFamily(local.Address) != GetFamily(remote.Address))
            {
                return false;
            }
            if (local.Component != remote.Component)
            {
                return false;
            }
            // UDP 只与 UDP 配对，跨传输协议的检查请求双方都收不到
            if (local.Transport != remote.Transport)
            {
                return false;
            }
            // 本地被动候选在等对端连接，本端不主动发起检查；TCP 方向矩阵细化前先剪除
            if (local.TcpType == TcpCandidateType.Passive)
            {
                return false;
            }
            // 链路本地地址出不了本链路，与全局地址配对注定失败
            if (local.IsLinkLocal != remote.IsLinkLocal)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 取配对用的地址族，v4-mapped 地址按 IPv4 处理
        /// </summary>
        /// <param name="address">候选地址，可为空</param>
        /// <returns>归一化后的地址族；空地址返回 Unknown，使其与任何有效地址都不相等</returns>
        private static AddressFamily GetFamily(IPAddress address)
        {
            if (address == null)
            {
                return AddressFamily.Unknown;
            }
            if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6)
            {
                return AddressFamily.InterNetwork;
            }
            return address.AddressFamily;
        }

        private bool IsDuplicate(Candidate local, Candidate remote)
        {
            for (int i = 0; i < _pairs.Count; i++)
            {
                CandidatePair pair = _pairs[i];
                if (pair.Local.Component != local.Component || pair.Local.Transport != local.Transport)
                {
                    continue;
                }
                if (pair.Local.EndPoint.Equals(local.EndPoint) && pair.Remote.EndPoint.Equals(remote.EndPoint))
                {
                    return true;
                }
            }
            return false;
        }

        private void UnfreezeGroup(string groupKey)
        {
            CandidatePair best = null;
            for (int i = 0; i < _pairs.Count; i++)
            {
                CandidatePair pair = _pairs[i];
                if (!string.Equals(GetGroupKey(pair), groupKey, StringComparison.Ordinal))
                {
                    continue;
                }

                // 组内已有等待或检查中的候选对，说明该组已解冻，不再提升新成员
                if (pair.State == PairState.Waiting || pair.State == PairState.InProgress)
                {
                    return;
                }
                if (pair.State == PairState.Frozen && (best == null || pair.Priority > best.Priority))
                {
                    best = pair;
                }
            }

            if (best == null)
            {
                return;
            }
            best.MarkWaiting();
        }

        private static string GetGroupKey(CandidatePair pair)
        {
            return GetGroupKey(pair.Local, pair.Remote);
        }

        private static string GetGroupKey(Candidate local, Candidate remote)
        {
            // 同 foundation 组的候选对共享 base 与类型，检查顺序按组推进
            return string.Concat(local.Foundation, "|", remote.Foundation);
        }
    }
}