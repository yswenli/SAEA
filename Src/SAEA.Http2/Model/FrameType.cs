/****************************************************************************
*项目名称：SAEA.Http2.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Model
*类 名 称：FrameType
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 16:30:43
*描述：
*=====================================================================
*修改时间：2019/6/27 16:30:43
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/

namespace SAEA.Http2.Model
{
    /// <summary>
    /// 可能的帧类型和HTTP/2的相关操作码
    /// </summary>
    public enum FrameType : byte
    {
        Data = 0x0,
        Headers = 0x1,
        Priority = 0x2,
        ResetStream = 0x3,
        Settings = 0x4,
        PushPromise = 0x5,
        Ping = 0x6,
        GoAway = 0x7,
        WindowUpdate = 0x8,
        Continuation = 0x9,
    }
}
