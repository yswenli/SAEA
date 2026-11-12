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
*文件名： IceSessionParameters
*版本号： v26.11.5.1
*唯一标识：92227e0b-86fc-45f8-973c-1554af10469c
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 14:44:17
*描述：ICE 会话参数
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 14:44:17
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 ICE 会话参数
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;

namespace SAEA.P2P.Ice
{
    /// <summary>
    /// ICE 会话参数：本端与对端的短凭据、本端角色、角色裁决值与对端候选集合。
    /// 对端候选可在会话启动前后增量加入，供两阶段 trickle 使用。
    /// </summary>
    public sealed class IceSessionParameters
    {
        /// <summary>
        /// 保护对端候选集合的锁，候选可来自信令回调线程
        /// </summary>
        private readonly object _syncRoot = new object();

        /// <summary>
        /// 对端候选集合，按加入顺序保存
        /// </summary>
        private readonly List<Candidate> _remoteCandidates = new List<Candidate>();

        /// <summary>
        /// 本端 ufrag
        /// </summary>
        public string LocalUfrag { get; set; }

        /// <summary>
        /// 本端口令
        /// </summary>
        public string LocalPassword { get; set; }

        /// <summary>
        /// 对端 ufrag
        /// </summary>
        public string RemoteUfrag { get; set; }

        /// <summary>
        /// 对端口令
        /// </summary>
        public string RemotePassword { get; set; }

        /// <summary>
        /// 本端角色，默认控制方
        /// </summary>
        public IceRole Role { get; set; } = IceRole.Controlling;

        /// <summary>
        /// 角色裁决值：双方角色冲突时取较大者作为控制方
        /// </summary>
        public ulong Tiebreaker { get; set; }

        /// <summary>
        /// 分量编号，默认 1
        /// </summary>
        public int Component { get; set; } = 1;

        /// <summary>
        /// 对端候选快照：返回复制出的一致性视图，可跨线程遍历
        /// </summary>
        public IReadOnlyList<Candidate> RemoteCandidates
        {
            get
            {
                lock (_syncRoot)
                {
                    return _remoteCandidates.ToArray();
                }
            }
        }

        /// <summary>
        /// 增量加入对端候选，供两阶段 trickle 使用
        /// </summary>
        /// <param name="candidate">对端候选</param>
        /// <exception cref="ArgumentNullException">候选为空</exception>
        public void AddRemoteCandidate(Candidate candidate)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            lock (_syncRoot)
            {
                for (int i = 0; i < _remoteCandidates.Count; i++)
                {
                    Candidate existing = _remoteCandidates[i];
                    // 传输、TCP 方向与端点三者相同视为同一候选：信令重发时只保留第一条
                    if (existing.Transport == candidate.Transport
                        && existing.TcpType == candidate.TcpType
                        && existing.EndPoint.Equals(candidate.EndPoint))
                    {
                        return;
                    }
                }

                _remoteCandidates.Add(candidate);
            }
        }

        /// <summary>
        /// 校验凭据与分量编号，供会话启动前调用
        /// </summary>
        /// <exception cref="ArgumentException">任一凭据为空或空串</exception>
        /// <exception cref="ArgumentOutOfRangeException">分量编号不在 1 到 256 之间</exception>
        internal void Validate()
        {
            if (string.IsNullOrEmpty(LocalUfrag))
            {
                throw new ArgumentException("本地 ufrag 不能为空", nameof(LocalUfrag));
            }
            if (string.IsNullOrEmpty(LocalPassword))
            {
                throw new ArgumentException("本地口令不能为空", nameof(LocalPassword));
            }
            if (string.IsNullOrEmpty(RemoteUfrag))
            {
                throw new ArgumentException("对端 ufrag 不能为空", nameof(RemoteUfrag));
            }
            if (string.IsNullOrEmpty(RemotePassword))
            {
                throw new ArgumentException("对端口令不能为空", nameof(RemotePassword));
            }
            if (Component < 1 || Component > 256)
            {
                throw new ArgumentOutOfRangeException(nameof(Component), "分量编号必须在 1 到 256 之间");
            }
        }
    }
}
