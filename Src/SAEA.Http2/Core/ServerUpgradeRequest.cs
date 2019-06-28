/****************************************************************************
*项目名称：SAEA.Http2.Core
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Core
*类 名 称：ServerUpgradeRequest
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 16:01:04
*描述：
*=====================================================================
*修改时间：2019/6/27 16:01:04
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Http2.Model;
using System.Collections.Generic;

namespace SAEA.Http2.Core
{
    /// <summary>
    /// 服务器升级请求
    /// </summary>
    public class ServerUpgradeRequest
    {
        internal readonly Settings Settings;

        internal readonly List<HeaderField> Headers;

        internal readonly byte[] Payload;

        private readonly bool valid;

        public bool IsValid => valid;

        internal ServerUpgradeRequest(
            Settings settings,
            List<HeaderField> headers,
            byte[] payload,
            bool valid)
        {
            this.Settings = settings;
            this.Headers = headers;
            this.Payload = payload;
            this.valid = valid;
        }
    }
}
