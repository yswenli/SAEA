/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.TcpP2P.Net
*文件名： HolePunchingType
*版本号： V3.3.3.4
*唯一标识：71bd11c0-378f-434a-9739-d843ecb5a0b7
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 21:04:48
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 21:04:48
*修改人： yswenli
*版本号： V3.3.3.4
*描述：
*
*****************************************************************************/

namespace SAEA.TcpP2P.Net
{
    public enum HolePunchingType
    {
        Heart = 0,
        PeerListRequest = 1,
        PeerListResponse = 11,
        Logout = 2,
        P2PRequest = 3,
        P2PSRequest = 31,
        P2PSResponse = 32,
        P2PResponse = 4,
        Message = 5,
        Close = 6
    }
}
