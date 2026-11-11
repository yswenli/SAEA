/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.P2P.Tests.Ice
*文件名： RoleConflictTests
*版本号： net10.0
*唯一标识：6ae9799f-e5b5-4c2e-90a8-859765a771be
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 15:30:00
*描述：ICE 角色冲突判定与 487 收敛用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 15:30:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 RoleConflictTests
*
*****************************************************************************/
using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Ice;

namespace SAEA.P2P.Tests.Ice
{
    /// <summary>
    /// ICE 角色冲突判定、487 收敛与 tiebreaker 换新用例。
    /// </summary>
    [TestClass]
    public class RoleConflictTests
    {
        /// <summary>
        /// 控制方 tiebreaker 大于等于对端时回 487 并保持控制方，相等边界同样回 487
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void RoleConflict_控制方tiebreaker较大_回487并保持()
        {
            // 本地 1000 不小于对端 900，冲突由对端让步，本地仍是控制方
            var conflict = new RoleConflict(IceRole.Controlling, 1000, () => 777UL);

            RoleConflictAction action = conflict.EvaluateIncomingRequest(IceRole.Controlling, 900);

            Assert.AreEqual(RoleConflictAction.Respond487, action);
            Assert.AreEqual(IceRole.Controlling, conflict.Role);
            Assert.AreEqual(1000UL, conflict.Tiebreaker);

            // 相等边界按「大于等于」判本地胜出，仍回 487 且状态不变
            action = conflict.EvaluateIncomingRequest(IceRole.Controlling, 1000);

            Assert.AreEqual(RoleConflictAction.Respond487, action);
            Assert.AreEqual(IceRole.Controlling, conflict.Role);
            Assert.AreEqual(1000UL, conflict.Tiebreaker);
        }

        /// <summary>
        /// 控制方 tiebreaker 小于对端时切换为受控
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void RoleConflict_控制方tiebreaker较小_切换为受控()
        {
            // 本地 1000 小于对端 1001，本地让出控制权
            var conflict = new RoleConflict(IceRole.Controlling, 1000, () => 777UL);

            RoleConflictAction action = conflict.EvaluateIncomingRequest(IceRole.Controlling, 1001);

            Assert.AreEqual(RoleConflictAction.SwitchRole, action);
            Assert.AreEqual(IceRole.Controlled, conflict.Role);
        }

        /// <summary>
        /// 收到 487 后切换角色并更换 tiebreaker，工厂恒定时也不会沿用旧值
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void RoleConflict_收到487_更换tiebreaker并重发()
        {
            // 工厂给出 777，切换后 tiebreaker 必须换成新值
            var conflict = new RoleConflict(IceRole.Controlling, 1000, () => 777UL);

            conflict.HandleRoleConflictResponse();

            Assert.AreEqual(IceRole.Controlled, conflict.Role);
            Assert.AreEqual(777UL, conflict.Tiebreaker);
            Assert.AreNotEqual(1000UL, conflict.Tiebreaker);

            // 再次收到 487 时角色回到控制方；当前值已是 777，工厂恒定也必须换新，用 +1 兜底得到 778
            conflict.HandleRoleConflictResponse();

            Assert.AreEqual(IceRole.Controlling, conflict.Role);
            Assert.AreNotEqual(777UL, conflict.Tiebreaker);

            // 工厂返回值与当前值恒等时必须兜底换新，否则重发会不断触发同一冲突
            var sameValue = new RoleConflict(IceRole.Controlling, 777, () => 777UL);

            sameValue.HandleRoleConflictResponse();

            Assert.AreNotEqual(777UL, sameValue.Tiebreaker);
        }

        /// <summary>
        /// 双方同时自认控制方时经 487 收敛为一方控制、一方受控
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public void RoleConflict_连续翻转_仍能收敛()
        {
            // 双方都从控制方起，b 的 tiebreaker 更大，应由 b 保持控制、a 让出
            var a = new RoleConflict(IceRole.Controlling, 10, () => 777UL);
            var b = new RoleConflict(IceRole.Controlling, 20, () => 888UL);

            try
            {
                for (int i = 0; i < 8; i++)
                {
                    RoleConflictAction action = b.EvaluateIncomingRequest(a.Role, a.Tiebreaker);
                    if (action == RoleConflictAction.None)
                    {
                        break;
                    }

                    // 只有收到 487 的一方才换角色与 tiebreaker 并重发
                    if (action == RoleConflictAction.Respond487)
                    {
                        a.HandleRoleConflictResponse();
                    }
                }
            }
            catch (Exception ex)
            {
                Assert.Fail("角色收敛过程抛出异常：" + ex);
            }

            // 收敛后双方角色必须不同，否则后续检查请求会一直互相回 487
            Assert.AreNotEqual(a.Role, b.Role);
        }
    }
}