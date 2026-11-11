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
*文件名： RoleConflict
*版本号： v26.11.5.1
*唯一标识：de5be9fb-e135-43d9-bdbd-6b05f42eda00
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 15:30:00
*描述：ICE 角色冲突判定与 487 收敛
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 15:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 ICE 角色冲突判定与 487 收敛
*
*****************************************************************************/
using System;
using System.Security.Cryptography;

namespace SAEA.P2P.Ice
{
    /// <summary>
    /// 角色冲突处理动作
    /// </summary>
    public enum RoleConflictAction
    {
        /// <summary>
        /// 无冲突，保持当前角色与 tiebreaker
        /// </summary>
        None = 0,

        /// <summary>
        /// 回 487 (Role Conflict) 响应并保持当前角色
        /// </summary>
        Respond487 = 1,

        /// <summary>
        /// 已切换本地角色，调用方无需再回 487
        /// </summary>
        SwitchRole = 2
    }

    /// <summary>
    /// ICE 角色冲突判定：按本地角色与 tiebreaker 大小决定回 487 还是切换角色，收到 487 时换新 tiebreaker
    /// </summary>
    public sealed class RoleConflict
    {
        // 工厂可能恒定返回同一个值，取有限次后改用当前值 + 1 兜底，避免死循环
        private const int MaxTiebreakerAttempts = 8;

        private readonly Func<ulong> _tiebreakerFactory;

        /// <summary>
        /// 创建角色冲突处理器，收到 487 时用随机 64 位工厂更换 tiebreaker
        /// </summary>
        /// <param name="role">本地初始角色</param>
        /// <param name="tiebreaker">本地初始 tiebreaker</param>
        public RoleConflict(IceRole role, ulong tiebreaker)
            : this(role, tiebreaker, CreateRandomTiebreaker)
        {
        }

        /// <summary>
        /// 创建角色冲突处理器并指定 tiebreaker 工厂
        /// </summary>
        /// <param name="role">本地初始角色</param>
        /// <param name="tiebreaker">本地初始 tiebreaker</param>
        /// <param name="tiebreakerFactory">tiebreaker 工厂，收到 487 换新值时调用</param>
        /// <exception cref="ArgumentNullException">tiebreakerFactory 为空</exception>
        public RoleConflict(IceRole role, ulong tiebreaker, Func<ulong> tiebreakerFactory)
        {
            if (tiebreakerFactory == null)
            {
                throw new ArgumentNullException(nameof(tiebreakerFactory));
            }

            _tiebreakerFactory = tiebreakerFactory;
            Role = role;
            Tiebreaker = tiebreaker;
        }

        /// <summary>
        /// 本地当前角色
        /// </summary>
        public IceRole Role { get; private set; }

        /// <summary>
        /// 本地当前 tiebreaker，收到 487 换新后随之变化
        /// </summary>
        public ulong Tiebreaker { get; private set; }

        /// <summary>
        /// 评估收到的检查请求所声明的对端角色，返回应执行的动作
        /// </summary>
        /// <param name="peerRole">对端在请求中声明的角色</param>
        /// <param name="peerTiebreaker">对端 tiebreaker，取自 ICE-CONTROLLING 或 ICE-CONTROLLED 属性</param>
        /// <returns>角色相同时按 tiebreaker 返回 <see cref="RoleConflictAction.Respond487"/> 或 <see cref="RoleConflictAction.SwitchRole"/>，不同则返回 <see cref="RoleConflictAction.None"/></returns>
        public RoleConflictAction EvaluateIncomingRequest(IceRole peerRole, ulong peerTiebreaker)
        {
            // 只有对端声明与自己相同的角色才算冲突，角色不同时状态保持不变
            if (peerRole != Role)
            {
                return RoleConflictAction.None;
            }

            // tiebreaker 大的一方胜出：控制方胜出则要求对端让步（回 487），受控方胜出则自己改当控制方
            bool localWins = Tiebreaker >= peerTiebreaker;
            if (Role == IceRole.Controlling)
            {
                if (localWins)
                {
                    return RoleConflictAction.Respond487;
                }

                Role = IceRole.Controlled;
                return RoleConflictAction.SwitchRole;
            }

            if (localWins)
            {
                Role = IceRole.Controlling;
                return RoleConflictAction.SwitchRole;
            }

            return RoleConflictAction.Respond487;
        }

        /// <summary>
        /// 处理收到的 487 (Role Conflict) 响应：切换角色并更换 tiebreaker，调用方负责把该 pair 放回 triggered-check 队列重发
        /// </summary>
        public void HandleRoleConflictResponse()
        {
            Role = Role == IceRole.Controlling ? IceRole.Controlled : IceRole.Controlling;
            Tiebreaker = NextTiebreaker();
        }

        /// <summary>
        /// 直接设定角色与 tiebreaker，用于信令交换后的初始化，tiebreaker 工厂保持不变
        /// </summary>
        /// <param name="role">要设定的角色</param>
        /// <param name="tiebreaker">要设定的 tiebreaker</param>
        public void SetRole(IceRole role, ulong tiebreaker)
        {
            Role = role;
            Tiebreaker = tiebreaker;
        }

        private ulong NextTiebreaker()
        {
            ulong current = Tiebreaker;
            for (int i = 0; i < MaxTiebreakerAttempts; i++)
            {
                ulong next = _tiebreakerFactory();
                if (next != current)
                {
                    return next;
                }
            }

            // 工厂恒定与当前值相同，加 1 保证换新生效
            return current + 1;
        }

        private static ulong CreateRandomTiebreaker()
        {
            // netstandard2.0 没有 RandomNumberGenerator.Fill，且每次调用都应拿到新值，故每次新建实例
            byte[] buffer = new byte[8];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(buffer);
            }

            return BitConverter.ToUInt64(buffer, 0);
        }
    }
}