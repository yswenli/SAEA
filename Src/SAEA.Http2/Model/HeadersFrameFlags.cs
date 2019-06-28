/****************************************************************************
*项目名称：SAEA.Http2.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Model
*类 名 称：HeadersFrameFlags
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 16:33:16
*描述：
*=====================================================================
*修改时间：2019/6/27 16:33:16
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;

namespace SAEA.Http2.Model
{
    [Flags]
    public enum HeadersFrameFlags : byte
    {
        EndOfStream = 0x01,

        EndOfHeaders = 0x4,

        Padded = 0x08,

        Priority = 0x20,
    }
}
