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
*文件名： IceTransportType
*版本号： v26.11.5.1
*唯一标识：d1a1b030-960b-4398-9caa-88c2ae8cea20
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 11:00:00
*描述：ICE 候选传输协议与 TCP 候选方向类型
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 11:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 ICE 传输类型与 TCP 候选方向类型
*
*****************************************************************************/
namespace SAEA.P2P.Ice
{
    /// <summary>
    /// ICE 候选使用的传输协议
    /// </summary>
    public enum IceTransportType
    {
        /// <summary>
        /// UDP 传输
        /// </summary>
        Udp = 1,

        /// <summary>
        /// TCP 传输
        /// </summary>
        Tcp = 2
    }

    /// <summary>
    /// TCP 候选的方向类型；UDP 候选固定为 None
    /// </summary>
    public enum TcpCandidateType
    {
        /// <summary>
        /// 非 TCP 候选，不参与 TCP 方向偏好计算
        /// </summary>
        None = 0,

        /// <summary>
        /// 主动连接对端的被动候选
        /// </summary>
        Active = 1,

        /// <summary>
        /// 被动监听，等待对端连接
        /// </summary>
        Passive = 2,

        /// <summary>
        /// 同时打开，两端同时发起连接
        /// </summary>
        SimultaneousOpen = 3
    }
}
