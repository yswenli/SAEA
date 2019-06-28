/****************************************************************************
*项目名称：SAEA.Http2
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2
*类 名 称：Constants
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 16:44:28
*描述：
*=====================================================================
*修改时间：2019/6/27 16:44:28
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;

namespace SAEA.Http2
{
    /// <summary>
    /// http2 包含恒定值
    /// </summary>
    static class Constants
    {
        public static readonly ArraySegment<byte> EmptyByteArray =
            new ArraySegment<byte>(new byte[0]);

        public const int InitialConnectionWindowSize = 65535;
    }
}
