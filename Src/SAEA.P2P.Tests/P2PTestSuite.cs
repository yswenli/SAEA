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
*命名空间：SAEA.P2P.Tests
*文件名： P2PTestSuite
*版本号： net10.0
*唯一标识：02be1421-0435-4e9c-8cfc-1ac0969a69ff
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/16 10:00:00
*描述：P2P 测试用例分组常量与验收入口
*
*=====================================================================
*修改标记
*修改时间：2026/11/16 10:00:00
*修改人： yswenli
*版本号： net10.0
*描述：新增用例分组常量
*
*****************************************************************************/

namespace SAEA.P2P.Tests
{
    /// <summary>
    /// P2P 测试用例分组常量，供各测试类的类级 <c>TestCategory</c> 特性引用。
    /// 按以下三条命令分组验收：
    /// 1. 全量：dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release
    /// 2. 单元类（不含矩阵）：dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "TestCategory=Unit"
    /// 3. 矩阵类：dotnet test Src/SAEA.P2P.Tests/SAEA.P2P.Tests.csproj -c Release --filter "TestCategory=Matrix"
    /// </summary>
    public static class P2PTestSuite
    {
        /// <summary>
        /// 单元级用例分组：编解码、协议头、状态机与本地装配类用例，不启动真实网络矩阵。
        /// </summary>
        public const string Unit = "Unit";

        /// <summary>
        /// NAT 模拟用例分组：NAT 盒、虚拟主机、NAT 烟雾测试与 STUN NAT 集成。
        /// </summary>
        public const string Nat = "Nat";

        /// <summary>
        /// 矩阵用例分组：UDP / TCP / TURN 传输矩阵与端到端组合，耗时最长的分组。
        /// </summary>
        public const string Matrix = "Matrix";

        /// <summary>
        /// IPv6 用例分组：双栈、IPv6 候选、IPv6 边界与跨地址族用例。
        /// </summary>
        public const string Ipv6 = "Ipv6";

        /// <summary>
        /// 本地网络用例分组：本地发现与本地网络集成用例。
        /// </summary>
        public const string LocalNet = "LocalNet";
    }
}
