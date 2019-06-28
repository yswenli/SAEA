/****************************************************************************
*项目名称：SAEA.Http2.Net
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Net
*类 名 称：Http2Result
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/28 17:29:31
*描述：
*=====================================================================
*修改时间：2019/6/28 17:29:31
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Http2.Model;
using System.Collections.Generic;

namespace SAEA.Http2.Net
{

    public class Http2Result
    {
        public List<HeaderField> Heads
        {
            get;
            set;
        }

        public byte[] Body
        {
            get;
            set;
        }
    }
}
