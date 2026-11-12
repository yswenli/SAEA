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
*文件名： ExpectedConnectivity
*版本号： net10.0
*唯一标识：207d4f8e-f7f1-44e6-934b-0329e20cecf8
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 21:00:00
*描述：NAT 类型组合下的连通性期望判定与矩阵用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 21:00:00
*修改人： yswenli
*版本号： net10.0
*描述：新增 NAT 矩阵期望判定与其用例
*
*****************************************************************************/
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SAEA.P2P.Tests.Ice
{
    /// <summary>
    /// 参与连通性判定的 NAT 行为类型
    /// </summary>
    public enum MatrixProfile
    {
        /// <summary>
        /// 完全锥形：任何外部端点都可向已建立的映射发入向包
        /// </summary>
        FullCone,

        /// <summary>
        /// 地址受限：只接受已通信过的地址的入向包
        /// </summary>
        Restricted,

        /// <summary>
        /// 端口受限：只接受已通信过的地址与端口的入向包
        /// </summary>
        PortRestricted,

        /// <summary>
        /// 对称型：每个目标端点单独分配映射，外部端口互不相同
        /// </summary>
        Symmetric
    }

    /// <summary>
    /// 打洞结果的期望形态
    /// </summary>
    public enum MatrixOutcome
    {
        /// <summary>
        /// 可直连
        /// </summary>
        Direct,

        /// <summary>
        /// 需中继转发
        /// </summary>
        Relay
    }

    /// <summary>
    /// 按双方 NAT 类型独立判定期望连通性，不依赖被判定实现的任何类型。
    /// </summary>
    public static class ExpectedConnectivity
    {
        /// <summary>
        /// 判定一对 NAT 类型的期望结果
        /// </summary>
        /// <param name="local">本端 NAT 类型</param>
        /// <param name="remote">对端 NAT 类型</param>
        /// <returns>直连或中继</returns>
        public static MatrixOutcome Evaluate(MatrixProfile local, MatrixProfile remote)
        {
            // 对端对称时其 srflx 端口只对 STUN 服务端有效，任何发往该端口的包都会被换成另一条映射；
            // 对端按端口过滤时，未先建立的映射收不到入向包
            if (IsSymmetric(local) && (IsSymmetric(remote) || remote == MatrixProfile.PortRestricted))
                return MatrixOutcome.Relay;

            if (IsSymmetric(remote) && FiltersByPort(local))
                return MatrixOutcome.Relay;

            return MatrixOutcome.Direct;
        }

        /// <summary>
        /// 判断该类型是否按端口过滤入向包
        /// </summary>
        /// <param name="profile">NAT 类型</param>
        /// <returns>端口受限或对称型时为 true</returns>
        public static bool FiltersByPort(MatrixProfile profile)
        {
            return profile == MatrixProfile.PortRestricted || profile == MatrixProfile.Symmetric;
        }

        /// <summary>
        /// 判断该类型是否为对称型
        /// </summary>
        /// <param name="profile">NAT 类型</param>
        /// <returns>对称型时为 true</returns>
        public static bool IsSymmetric(MatrixProfile profile)
        {
            return profile == MatrixProfile.Symmetric;
        }
    }

    /// <summary>
    /// NAT 类型两两组合的期望连通性矩阵用例。
    /// </summary>
    [TestClass]
    public class ExpectedConnectivityTests
    {
        /// <summary>
        /// 16 格期望值逐条硬写，避免用被测方法自身反推
        /// </summary>
        [TestMethod]
        public void ExpectedConnectivity_矩阵表逐格与设计一致()
        {
            // 完全锥形行：四格全部直连
            Assert.AreEqual(MatrixOutcome.Direct, ExpectedConnectivity.Evaluate(MatrixProfile.FullCone, MatrixProfile.FullCone), "FC×FC 应直连");
            Assert.AreEqual(MatrixOutcome.Direct, ExpectedConnectivity.Evaluate(MatrixProfile.FullCone, MatrixProfile.Restricted), "FC×R 应直连");
            Assert.AreEqual(MatrixOutcome.Direct, ExpectedConnectivity.Evaluate(MatrixProfile.FullCone, MatrixProfile.PortRestricted), "FC×PR 应直连");
            Assert.AreEqual(MatrixOutcome.Direct, ExpectedConnectivity.Evaluate(MatrixProfile.FullCone, MatrixProfile.Symmetric), "FC×S 应直连");

            // 地址受限行：四格全部直连
            Assert.AreEqual(MatrixOutcome.Direct, ExpectedConnectivity.Evaluate(MatrixProfile.Restricted, MatrixProfile.FullCone), "R×FC 应直连");
            Assert.AreEqual(MatrixOutcome.Direct, ExpectedConnectivity.Evaluate(MatrixProfile.Restricted, MatrixProfile.Restricted), "R×R 应直连");
            Assert.AreEqual(MatrixOutcome.Direct, ExpectedConnectivity.Evaluate(MatrixProfile.Restricted, MatrixProfile.PortRestricted), "R×PR 应直连");
            Assert.AreEqual(MatrixOutcome.Direct, ExpectedConnectivity.Evaluate(MatrixProfile.Restricted, MatrixProfile.Symmetric), "R×S 应直连");

            // 端口受限行：仅对端为对称型时中继
            Assert.AreEqual(MatrixOutcome.Direct, ExpectedConnectivity.Evaluate(MatrixProfile.PortRestricted, MatrixProfile.FullCone), "PR×FC 应直连");
            Assert.AreEqual(MatrixOutcome.Direct, ExpectedConnectivity.Evaluate(MatrixProfile.PortRestricted, MatrixProfile.Restricted), "PR×R 应直连");
            Assert.AreEqual(MatrixOutcome.Direct, ExpectedConnectivity.Evaluate(MatrixProfile.PortRestricted, MatrixProfile.PortRestricted), "PR×PR 应直连");
            Assert.AreEqual(MatrixOutcome.Relay, ExpectedConnectivity.Evaluate(MatrixProfile.PortRestricted, MatrixProfile.Symmetric), "PR×S 应中继");

            // 对称型行：对端为端口受限或对称型时中继
            Assert.AreEqual(MatrixOutcome.Direct, ExpectedConnectivity.Evaluate(MatrixProfile.Symmetric, MatrixProfile.FullCone), "S×FC 应直连");
            Assert.AreEqual(MatrixOutcome.Direct, ExpectedConnectivity.Evaluate(MatrixProfile.Symmetric, MatrixProfile.Restricted), "S×R 应直连");
            Assert.AreEqual(MatrixOutcome.Relay, ExpectedConnectivity.Evaluate(MatrixProfile.Symmetric, MatrixProfile.PortRestricted), "S×PR 应中继");
            Assert.AreEqual(MatrixOutcome.Relay, ExpectedConnectivity.Evaluate(MatrixProfile.Symmetric, MatrixProfile.Symmetric), "S×S 应中继");
        }
    }
}
