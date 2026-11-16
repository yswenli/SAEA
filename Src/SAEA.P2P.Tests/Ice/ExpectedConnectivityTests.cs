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
*文件名： ExpectedConnectivityTests
*版本号： net10.0
*唯一标识：673bd5b6-5f8a-4a52-a628-777324947bf2
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 11:50:40
*描述：NAT 矩阵期望判定表的逐格用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 11:50:40
*修改人： yswenli
*版本号： net10.0
*描述：从 ExpectedConnectivity.cs 拆出独立文件
*
*****************************************************************************/
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SAEA.P2P.Tests.Ice
{
    /// <summary>
    /// NAT 类型两两组合的期望连通性矩阵用例。
    /// </summary>
    [TestCategory(P2PTestSuite.Unit)]
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
