/****************************************************************************
*项目名称：SAEA.Http2.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Model
*类 名 称：StreamReadResult
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 16:23:12
*描述：
*=====================================================================
*修改时间：2019/6/27 16:23:12
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/

namespace SAEA.Http2.Model
{
    /// <summary>
    /// readasync操作的结果
    /// </summary>
    public struct StreamReadResult
    {
        public int BytesRead;

        public bool EndOfStream;
    }
}
