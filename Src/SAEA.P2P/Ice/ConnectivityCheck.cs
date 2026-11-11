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
*文件名： ConnectivityCheck
*版本号： v26.11.5.1
*唯一标识：4b8673c0-0f25-464f-9d3f-5710ab292cbc
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 16:30:00
*描述：ICE 连通性检查——请求属性构造、结果映射与中继权限先行
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 16:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 ICE 连通性检查
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
    /// 连通性检查结果：成功与否、是否角色冲突、映射地址与原始 Binding 结果。
    /// </summary>
    public sealed class ConnectivityCheckResult
    {
        /// <summary>
        /// 由连通性检查填充结果，不对外公开构造
        /// </summary>
        /// <param name="succeeded">成功响应且完整性校验通过</param>
        /// <param name="roleConflict">是否为 487 角色冲突</param>
        /// <param name="mappedAddress">对端看到的本地映射地址</param>
        /// <param name="binding">原始 Binding 结果</param>
        internal ConnectivityCheckResult(bool succeeded, bool roleConflict, IPEndPoint mappedAddress, StunBindingResult binding)
        {
            Succeeded = succeeded;
            RoleConflict = roleConflict;
            MappedAddress = mappedAddress;
            Binding = binding;
        }

        /// <summary>
        /// 成功响应且（要求时）完整性校验通过
        /// </summary>
        public bool Succeeded { get; }

        /// <summary>
        /// 是否收到 487，调用方据此切换角色并换 tiebreaker 重发
        /// </summary>
        public bool RoleConflict { get; }

        /// <summary>
        /// 映射地址：成功时优先 XOR-MAPPED-ADDRESS，其次 MAPPED-ADDRESS；失败时为 null
        /// </summary>
        public IPEndPoint MappedAddress { get; }

        /// <summary>
        /// 原始 Binding 结果，含失败原因、错误码与耗时
        /// </summary>
        public StunBindingResult Binding { get; }
    }

    /// <summary>
    /// ICE 连通性检查：按本地 ufrag/pwd 构造检查请求，在对端候选上发 STUN Binding 并映射结果。
    /// 中继候选必须先建权限再发报文，否则 TURN 侧会丢弃这些报文。
    /// </summary>
    public sealed class ConnectivityCheck : IDisposable
    {
        /// <summary>
        /// 本地 ufrag，写入 USERNAME 的冒号后半段
        /// </summary>
        private readonly string _localUfrag;

        /// <summary>
        /// 本地口令；仅供对端校验本地发起或应答的完整性，本类不读取其内容
        /// </summary>
        private string _localPassword;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 用本地会话凭据创建连通性检查
        /// </summary>
        /// <param name="localUfrag">本地 ufrag，非空</param>
        /// <param name="localPassword">本地口令，非空；不进日志与异常消息</param>
        /// <exception cref="ArgumentException">ufrag 或口令为空</exception>
        public ConnectivityCheck(string localUfrag, string localPassword)
        {
            if (string.IsNullOrEmpty(localUfrag))
            {
                throw new ArgumentException("本地 ufrag 不能为空", nameof(localUfrag));
            }
            if (string.IsNullOrEmpty(localPassword))
            {
                throw new ArgumentException("本地口令不能为空", nameof(localPassword));
            }

            _localUfrag = localUfrag;
            _localPassword = localPassword;
        }

        /// <summary>
        /// 本地 ufrag
        /// </summary>
        public string LocalUfrag
        {
            get { return _localUfrag; }
        }

        /// <summary>
        /// 计算 peer-reflexive 候选的优先级；检查请求必须携带该值而不是本地候选真实类型的值
        /// </summary>
        /// <param name="component">分量编号，取值 1 到 256</param>
        /// <returns>候选优先级</returns>
        public uint ComputePeerReflexivePriority(int component)
        {
            return IcePriority.ComputeForUdp(CandidateType.PeerReflexive, component);
        }

        /// <summary>
        /// 构造检查请求属性，顺序固定为 USERNAME、PRIORITY、角色、USE-CANDIDATE
        /// </summary>
        /// <param name="remoteUfrag">对端 ufrag</param>
        /// <param name="role">本地角色，决定携带 ICE-CONTROLLING 还是 ICE-CONTROLLED</param>
        /// <param name="tiebreaker">角色冲突判定用的 64 位随机数</param>
        /// <param name="priority">按 peer-reflexive 类型算出的候选优先级</param>
        /// <param name="useCandidate">是否同时提名该候选对</param>
        /// <returns>请求属性列表</returns>
        /// <exception cref="ArgumentException">对端 ufrag 为空或角色不是枚举已声明的值</exception>
        public IReadOnlyList<StunAttribute> BuildRequestAttributes(string remoteUfrag, IceRole role, ulong tiebreaker, uint priority, bool useCandidate)
        {
            if (string.IsNullOrEmpty(remoteUfrag))
            {
                throw new ArgumentException("对端 ufrag 不能为空", nameof(remoteUfrag));
            }

            var attributes = new List<StunAttribute>();
            attributes.Add(new StunAttribute(StunAttributeType.Username, Encoding.UTF8.GetBytes(BuildUserName(remoteUfrag))));
            attributes.AddRange(BuildExtraAttributes(role, tiebreaker, priority, useCandidate));
            return attributes;
        }

        /// <summary>
        /// 在对端候选上做一次检查；中继候选会先建权限再发首个报文
        /// </summary>
        /// <param name="transport">已绑定的 UDP 传输，本类不负责释放</param>
        /// <param name="remoteEndPoint">对端候选端点</param>
        /// <param name="remoteUfrag">对端 ufrag</param>
        /// <param name="remotePassword">对端口令，用于计算请求与校验应答的 MESSAGE-INTEGRITY</param>
        /// <param name="role">本地角色</param>
        /// <param name="tiebreaker">角色冲突判定用的 64 位随机数</param>
        /// <param name="priority">按 peer-reflexive 类型算出的候选优先级</param>
        /// <param name="useCandidate">是否同时提名该候选对</param>
        /// <param name="permission">中继权限；为 null 表示不经中继，不需要建权限</param>
        /// <param name="remoteAddresses">对端候选的 IP 列表，permission 非空时必填</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>检查结果</returns>
        /// <exception cref="ArgumentNullException">transport、remoteEndPoint 为空，或 permission 非空但未给出地址列表</exception>
        /// <exception cref="ArgumentException">ufrag 或口令为空</exception>
        /// <exception cref="ObjectDisposedException">本实例已释放</exception>
        public async Task<ConnectivityCheckResult> CheckAsync(
            IUdpTransport transport,
            IPEndPoint remoteEndPoint,
            string remoteUfrag,
            string remotePassword,
            IceRole role,
            ulong tiebreaker,
            uint priority,
            bool useCandidate,
            ITurnRelayPermission permission,
            IReadOnlyList<IPAddress> remoteAddresses,
            CancellationToken cancellationToken)
        {
            if (transport == null)
            {
                throw new ArgumentNullException(nameof(transport));
            }
            if (remoteEndPoint == null)
            {
                throw new ArgumentNullException(nameof(remoteEndPoint));
            }
            if (string.IsNullOrEmpty(remoteUfrag))
            {
                throw new ArgumentException("对端 ufrag 不能为空", nameof(remoteUfrag));
            }
            if (string.IsNullOrEmpty(remotePassword))
            {
                throw new ArgumentException("对端口令不能为空", nameof(remotePassword));
            }
            if (permission != null && remoteAddresses == null)
            {
                throw new ArgumentNullException(nameof(remoteAddresses));
            }

            ThrowIfDisposed();

            var userName = BuildUserName(remoteUfrag);
            var extraAttributes = BuildExtraAttributes(role, tiebreaker, priority, useCandidate);

            // 中继侧没有权限时会把发往该地址的报文直接丢弃，所以权限必须早于首个检查包
            if (permission != null)
            {
                await permission.CreatePermissionsAsync(remoteAddresses, cancellationToken).ConfigureAwait(false);
            }

            StunBindingResult binding;
            using (var client = new StunClient(transport))
            {
                // ICE 用短凭据，首个请求即带 MESSAGE-INTEGRITY，没有 401 挑战可重试
                binding = await client.BindingAsync(
                    remoteEndPoint,
                    userName,
                    StunIntegrity.CreateShortTermKey(remotePassword),
                    true,
                    extraAttributes,
                    cancellationToken).ConfigureAwait(false);
            }

            // 401/438 表示对端口令不匹配，短凭据下没有长凭据挑战可跑，直接按失败上报
            var succeeded = binding.Succeeded;
            var roleConflict = !succeeded && binding.ErrorCode == 487;
            var mappedAddress = succeeded ? (binding.XorMappedAddress ?? binding.MappedAddress) : null;

            return new ConnectivityCheckResult(succeeded, roleConflict, mappedAddress, binding);
        }

        /// <summary>
        /// 释放时断开本地口令引用；string 不可变，无法真正擦除其内容
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            // string 不可变，置空只能让本实例不再持有引用，已复制的字符不会被覆盖
            _localPassword = null;
        }

        /// <summary>
        /// 拼接 STUN 短凭据用户名：对端 ufrag 在前，本地 ufrag 在后
        /// </summary>
        /// <param name="remoteUfrag">对端 ufrag</param>
        /// <returns>USERNAME 属性值</returns>
        private string BuildUserName(string remoteUfrag)
        {
            return remoteUfrag + ":" + _localUfrag;
        }

        /// <summary>
        /// 构造除 USERNAME 之外的请求属性；USERNAME 由 STUN 层按用户名写入，这里重复添加会让报文非法
        /// </summary>
        /// <param name="role">本地角色</param>
        /// <param name="tiebreaker">角色冲突判定用的 64 位随机数</param>
        /// <param name="priority">候选优先级</param>
        /// <param name="useCandidate">是否提名</param>
        /// <returns>额外属性列表</returns>
        /// <exception cref="ArgumentException">角色不是枚举已声明的值</exception>
        private static IReadOnlyList<StunAttribute> BuildExtraAttributes(IceRole role, ulong tiebreaker, uint priority, bool useCandidate)
        {
            var attributes = new List<StunAttribute>();
            attributes.Add(new StunAttribute(StunAttributeType.Priority, EncodeUInt32(priority)));
            attributes.Add(new StunAttribute(GetRoleAttributeType(role), EncodeUInt64(tiebreaker)));

            if (useCandidate)
            {
                // USE-CANDIDATE 是空值属性，语义全在属性类型上
                attributes.Add(new StunAttribute(StunAttributeType.UseCandidate, new byte[0]));
            }

            return attributes;
        }

        /// <summary>
        /// 取角色对应的属性类型；两个角色标记互斥，每个请求必带其一
        /// </summary>
        /// <param name="role">本地角色</param>
        /// <returns>角色属性类型</returns>
        /// <exception cref="ArgumentException">角色不是枚举已声明的值</exception>
        private static StunAttributeType GetRoleAttributeType(IceRole role)
        {
            if (role == IceRole.Controlling)
            {
                return StunAttributeType.IceControlling;
            }
            if (role == IceRole.Controlled)
            {
                return StunAttributeType.IceControlled;
            }

            throw new ArgumentException("角色只能是控制方或受控方", nameof(role));
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
        /// 已释放时拒绝新的检查
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
