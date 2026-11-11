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
*文件名： Nominator
*版本号： v26.11.5.1
*唯一标识：7b8329f7-8766-438a-82d0-f995d3dced87
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 17:00:00
*描述：ICE regular 提名、选中对发布与 peer-reflexive 候选创建
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 17:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 ICE 提名与 peer-reflexive 候选创建
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Stun;
using SAEA.P2P.Transport;

namespace SAEA.P2P.Ice
{
    /// <summary>
    /// 提名器：控制方对已成功的候选对再发一次带 USE-CANDIDATE 的 Binding 请求完成 regular 提名，
    /// 成功后才发布 <see cref="SelectedPair"/>，业务数据只允许在发布的 pair 上发送。
    /// 被动侧不发起提名，也不发布选中对。
    /// </summary>
    public sealed class Nominator : IDisposable
    {
        /// <summary>
        /// 内部持有的连通性检查，负责实际收发与完整性处理
        /// </summary>
        private readonly ConnectivityCheck _check;

        /// <summary>
        /// 所属检查表，提名成功后通知其停止接受远端候选
        /// </summary>
        private readonly CheckList _checkList;

        /// <summary>
        /// 本地 ufrag，写入 USERNAME 的冒号后半段
        /// </summary>
        private readonly string _localUfrag;

        /// <summary>
        /// 对端 ufrag，写入 USERNAME 的冒号前半段
        /// </summary>
        private readonly string _remoteUfrag;

        /// <summary>
        /// 对端口令，用于签发提名请求的 MESSAGE-INTEGRITY；不进日志与异常消息
        /// </summary>
        private string _remotePassword;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 创建提名器
        /// </summary>
        /// <param name="role">本地角色，只有控制方可以提名</param>
        /// <param name="tiebreaker">角色冲突判定用的 64 位随机数</param>
        /// <param name="localUfrag">本地 ufrag，非空</param>
        /// <param name="localPassword">本地口令，非空；不进日志与异常消息</param>
        /// <param name="remoteUfrag">对端 ufrag，非空</param>
        /// <param name="remotePassword">对端口令，非空；不进日志与异常消息</param>
        /// <param name="checkList">所属检查表，非空</param>
        /// <exception cref="ArgumentNullException">检查表为空</exception>
        /// <exception cref="ArgumentException">四个 ufrag 或口令参数中任一为空</exception>
        public Nominator(
            IceRole role,
            ulong tiebreaker,
            string localUfrag,
            string localPassword,
            string remoteUfrag,
            string remotePassword,
            CheckList checkList)
        {
            if (checkList == null)
            {
                throw new ArgumentNullException(nameof(checkList));
            }
            // 凭据为空的异常只带参数名，绝不把口令值拼进消息
            if (string.IsNullOrEmpty(localUfrag))
            {
                throw new ArgumentException("本地 ufrag 不能为空", nameof(localUfrag));
            }
            if (string.IsNullOrEmpty(localPassword))
            {
                throw new ArgumentException("本地口令不能为空", nameof(localPassword));
            }
            if (string.IsNullOrEmpty(remoteUfrag))
            {
                throw new ArgumentException("对端 ufrag 不能为空", nameof(remoteUfrag));
            }
            if (string.IsNullOrEmpty(remotePassword))
            {
                throw new ArgumentException("对端口令不能为空", nameof(remotePassword));
            }

            Role = role;
            Tiebreaker = tiebreaker;
            _localUfrag = localUfrag;
            _remoteUfrag = remoteUfrag;
            _remotePassword = remotePassword;
            _checkList = checkList;
            _check = new ConnectivityCheck(localUfrag, localPassword);
        }

        /// <summary>
        /// 本地角色
        /// </summary>
        public IceRole Role { get; }

        /// <summary>
        /// 角色冲突判定用的 64 位随机数
        /// </summary>
        public ulong Tiebreaker { get; }

        /// <summary>
        /// 是否为控制方；只有控制方可以发起提名
        /// </summary>
        public bool IsControlling
        {
            get { return Role == IceRole.Controlling; }
        }

        /// <summary>
        /// 已选中的候选对；未完成提名前为 null
        /// </summary>
        public SelectedPair SelectedPair { get; private set; }

        /// <summary>
        /// 提名是否已完成；完成后不再重复触发选中事件
        /// </summary>
        public bool IsNominationCompleted { get; private set; }

        /// <summary>
        /// 成功选出候选对时触发一次
        /// </summary>
        public event Action<SelectedPair> OnPairSelected;

        /// <summary>
        /// 构造一次提名请求：全新事务 ID 的 Binding 请求，携带 USERNAME、PRIORITY、ICE-CONTROLLING、USE-CANDIDATE 与 MESSAGE-INTEGRITY
        /// </summary>
        /// <param name="pair">待提名的候选对</param>
        /// <returns>可直接发送的提名请求</returns>
        /// <exception cref="ArgumentNullException">候选对为空</exception>
        /// <exception cref="InvalidOperationException">本地不是控制方</exception>
        /// <exception cref="ObjectDisposedException">本实例已释放</exception>
        public StunMessage CreateNominationMessage(CandidatePair pair)
        {
            if (pair == null)
            {
                throw new ArgumentNullException(nameof(pair));
            }
            if (Role != IceRole.Controlling)
            {
                throw new InvalidOperationException("只有控制方可以发起提名");
            }

            ThrowIfDisposed();

            // regular 提名要求全新事务 ID，因此这里不传事务 ID 让构造器生成；
            // 复用上一次检查请求的 ID 会被对端当成重传，拿不到独立的应答
            var message = new StunMessage(StunMessageType.Binding, StunClass.Request);

            var attributes = new List<StunAttribute>();
            attributes.Add(new StunAttribute(StunAttributeType.Username, Encoding.UTF8.GetBytes(_remoteUfrag + ":" + _localUfrag)));
            attributes.Add(new StunAttribute(StunAttributeType.Priority, EncodeUInt32(_check.ComputePeerReflexivePriority(pair.Local.Component))));
            attributes.Add(new StunAttribute(StunAttributeType.IceControlling, EncodeUInt64(Tiebreaker)));
            attributes.Add(new StunAttribute(StunAttributeType.UseCandidate, new byte[0]));
            message.SetAttributes(attributes);

            // 短凭据没有 401 挑战可重试，首个请求就要带完整性，密钥直接取对端口令的 UTF-8 字节
            StunIntegrity.Append(message, StunIntegrity.CreateShortTermKey(_remotePassword));

            return message;
        }

        /// <summary>
        /// 对已成功的候选对发起 regular 提名，成功后才发布选中对
        /// </summary>
        /// <param name="transport">已绑定的 UDP 传输，本类不负责释放</param>
        /// <param name="pair">待提名的候选对，必须已检查成功</param>
        /// <param name="permission">中继权限；为 null 表示不经中继</param>
        /// <param name="remoteAddresses">对端候选的 IP 列表，permission 非空时必填</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>提名成功或此前已完成提名返回 true，否则返回 false</returns>
        /// <exception cref="ArgumentNullException">transport 或 pair 为空</exception>
        /// <exception cref="InvalidOperationException">候选对未检查成功，或本实例已释放</exception>
        public async Task<bool> NominateAsync(
            IUdpTransport transport,
            CandidatePair pair,
            ITurnRelayPermission permission,
            IReadOnlyList<IPAddress> remoteAddresses,
            CancellationToken cancellationToken)
        {
            if (transport == null)
            {
                throw new ArgumentNullException(nameof(transport));
            }
            if (pair == null)
            {
                throw new ArgumentNullException(nameof(pair));
            }

            // 被动侧只能等对端提名，角色判定放在最前，避免误发报文或改动任何状态
            if (Role != IceRole.Controlling)
            {
                return false;
            }

            ThrowIfDisposed();

            // 对未成功的候选对提名，等于把没通过检查的路径当成品，必须先失败
            if (pair.State != PairState.Succeeded)
            {
                throw new InvalidOperationException(string.Format("只有检查成功的候选对才能提名，当前状态为 {0}", pair.State));
            }

            // 提名只发布一次选中对，重复调用沿用首次结果，不重发也不重复触发事件
            if (IsNominationCompleted)
            {
                return true;
            }

            var result = await _check.CheckAsync(
                transport,
                pair.Remote.EndPoint,
                _remoteUfrag,
                _remotePassword,
                Role,
                Tiebreaker,
                _check.ComputePeerReflexivePriority(pair.Local.Component),
                true,
                permission,
                remoteAddresses,
                cancellationToken).ConfigureAwait(false);

            if (!result.Succeeded)
            {
                return false;
            }

            pair.MarkNominated();

            var selected = new SelectedPair(pair);
            SelectedPair = selected;
            IsNominationCompleted = true;
            _checkList.MarkNominationCompleted();
            RaiseOnPairSelected(selected);

            return true;
        }

        /// <summary>
        /// 校验是否已选出可用候选对；业务数据只允许在提名成功后的 pair 上发送
        /// </summary>
        /// <exception cref="InvalidOperationException">尚未选出可用候选对</exception>
        public void EnsureCanSend()
        {
            if (SelectedPair == null)
            {
                throw new InvalidOperationException("尚未选出可用候选对，不能发送业务数据");
            }
        }

        /// <summary>
        /// 收到检查请求时创建 peer-reflexive 远端候选：对端端点不在已知远端候选里才新建
        /// </summary>
        /// <param name="source">检查请求的来源端点</param>
        /// <param name="priority">请求 PRIORITY 属性给出的优先级</param>
        /// <param name="component">分量编号，取值 1 到 256</param>
        /// <param name="transport">传输协议，跟随该检查通道</param>
        /// <param name="knownRemoteCandidates">已知远端候选，为 null 视为空</param>
        /// <returns>新建的 peer-reflexive 候选；端点已存在时返回 null</returns>
        /// <exception cref="ArgumentNullException">来源端点为空</exception>
        public static Candidate CreatePeerReflexiveFromRequest(
            IPEndPoint source,
            uint priority,
            int component,
            IceTransportType transport,
            IReadOnlyList<Candidate> knownRemoteCandidates)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            // 去重只看地址与端口：同一端点重复出现说明它早已是已知远端候选
            if (ContainsEndPoint(knownRemoteCandidates, source))
            {
                return null;
            }

            return new Candidate(CandidateType.PeerReflexive, transport, component, source.Address, source.Port, priority);
        }

        /// <summary>
        /// 收到检查响应时创建 peer-reflexive 本地候选：映射地址不匹配任何已知本地候选才新建，base 取该候选对的本地候选
        /// </summary>
        /// <param name="pair">产生该响应的候选对</param>
        /// <param name="mappedAddress">响应给出的本地映射地址</param>
        /// <param name="priority">请求 PRIORITY 属性给出的优先级</param>
        /// <param name="knownLocalCandidates">已知本地候选，为 null 视为空</param>
        /// <returns>新建的 peer-reflexive 候选；映射地址已存在时返回 null</returns>
        /// <exception cref="ArgumentNullException">候选对或映射地址为空</exception>
        public static Candidate CreatePeerReflexiveFromResponse(
            CandidatePair pair,
            IPEndPoint mappedAddress,
            uint priority,
            IReadOnlyList<Candidate> knownLocalCandidates)
        {
            if (pair == null)
            {
                throw new ArgumentNullException(nameof(pair));
            }
            if (mappedAddress == null)
            {
                throw new ArgumentNullException(nameof(mappedAddress));
            }

            if (ContainsEndPoint(knownLocalCandidates, mappedAddress))
            {
                return null;
            }

            // peer-reflexive 只由 UDP 检查过程产生，这里按 None 传方向类型不会触发 TCP 方向校验；
            // 已知限制：Candidate.Foundation 按候选自身地址计算，peer-reflexive 的 base 只能靠 related 字段保留，
            // 与 RFC 8445 取 base 计算 foundation 的口径存在差异；候选对去重按端点进行，不受影响
            return new Candidate(
                CandidateType.PeerReflexive,
                pair.Local.Transport,
                pair.Local.Component,
                mappedAddress.Address,
                mappedAddress.Port,
                TcpCandidateType.None,
                pair.Local.Address,
                pair.Local.Port);
        }

        /// <summary>
        /// 释放内部连通性检查并断开口令引用；重复调用不抛异常
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            _check.Dispose();

            // string 不可变，置空只能让本实例不再持有引用，已复制的字符不会被覆盖
            _remotePassword = null;
        }

        /// <summary>
        /// 触发选中事件
        /// </summary>
        /// <param name="pair">选中的候选对</param>
        private void RaiseOnPairSelected(SelectedPair pair)
        {
            var handler = OnPairSelected;
            if (handler != null)
            {
                handler(pair);
            }
        }

        /// <summary>
        /// 判断已知候选里是否有地址与端口都等于给定端点的成员
        /// </summary>
        /// <param name="candidates">已知候选，为 null 视为空</param>
        /// <param name="endPoint">待比较的端点</param>
        /// <returns>存在相同端点返回 true</returns>
        private static bool ContainsEndPoint(IReadOnlyList<Candidate> candidates, IPEndPoint endPoint)
        {
            if (candidates == null)
            {
                return false;
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate != null && candidate.HasSameAddress(endPoint))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 把 32 位无符号整数按网络字节序编码
        /// </summary>
        /// <param name="value">待编码的数值</param>
        /// <returns>4 字节大端数组</returns>
        private static byte[] EncodeUInt32(uint value)
        {
            var bytes = new byte[4];
            bytes[0] = (byte)(value >> 24);
            bytes[1] = (byte)(value >> 16);
            bytes[2] = (byte)(value >> 8);
            bytes[3] = (byte)value;
            return bytes;
        }

        /// <summary>
        /// 把 64 位无符号整数按网络字节序编码
        /// </summary>
        /// <param name="value">待编码的数值</param>
        /// <returns>8 字节大端数组</returns>
        private static byte[] EncodeUInt64(ulong value)
        {
            var bytes = new byte[8];
            bytes[0] = (byte)(value >> 56);
            bytes[1] = (byte)(value >> 48);
            bytes[2] = (byte)(value >> 40);
            bytes[3] = (byte)(value >> 32);
            bytes[4] = (byte)(value >> 24);
            bytes[5] = (byte)(value >> 16);
            bytes[6] = (byte)(value >> 8);
            bytes[7] = (byte)value;
            return bytes;
        }

        /// <summary>
        /// 已释放时拒绝新的提名
        /// </summary>
        /// <exception cref="ObjectDisposedException">本实例已释放</exception>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}