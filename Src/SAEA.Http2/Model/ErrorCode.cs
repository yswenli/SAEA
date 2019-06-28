/****************************************************************************
*项目名称：SAEA.Http2.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Model
*类 名 称：ErrorCode
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 16:05:39
*描述：
*=====================================================================
*修改时间：2019/6/27 16:05:39
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/

namespace SAEA.Http2.Model
{
    /// <summary>
    /// HTTP/2标准化的错误代码
    /// </summary>
    public enum ErrorCode : uint
    {
        NoError = 0x0,
        ProtocolError = 0x1,
        InternalError = 0x2,
        FlowControlError = 0x3,
        SettingsTimeout = 0x4,
        StreamClosed = 0x5,
        FrameSizeError = 0x6,
        RefusedStream = 0x7,
        Cancel = 0x8,
        CompressionError = 0x9,
        ConnectError = 0xa,
        EnhanceYourCalm = 0xb,
        InadequateSecurity = 0xc,
        Http11Required = 0xd,
    }
}
