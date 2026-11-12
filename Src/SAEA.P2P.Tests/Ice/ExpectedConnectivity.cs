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
*描述：NAT 类型组合的独立期望判定
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 11:50:40
*修改人： yswenli
*版本号： net10.0
*描述：拆出枚举与用例，本文件只保留判定逻辑
*
*****************************************************************************/
namespace SAEA.P2P.Tests.Ice
{
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
}
