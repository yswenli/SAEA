/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.NatSocket
*文件名： Class1
*版本号： V3.3.3.3
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： V3.3.3.3
*描述：
*
*****************************************************************************/
namespace SAEA.NatSocket.Pmp
{
    internal static class PmpConstants
    {
        public const byte Version = 0;

        public const byte OperationExternalAddressRequest = 0;

        public const byte OperationCodeUdp = 1;

        public const byte OperationCodeTcp = 2;

        public const byte ServerNoop = 128;

        public const int ClientPort = 5350;

        public const int ServerPort = 5351;

        public const int RetryDelay = 250;
        public const int RetryAttempts = 9;

        public const int RecommendedLeaseTime = 60 * 60;

        public const int DefaultLeaseTime = RecommendedLeaseTime;

        public const short ResultCodeSuccess = 0;

        public const short ResultCodeUnsupportedVersion = 1;

        public const short ResultCodeNotAuthorized = 2;

        public const short ResultCodeNetworkFailure = 3;

        public const short ResultCodeOutOfResources = 4;

        public const short ResultCodeUnsupportedOperationCode = 5;
    }
}
