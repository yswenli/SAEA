/****************************************************************************
*项目名称：SAEA.Http2.Core
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Core
*类 名 称：ClientUpgradeRequest
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 16:07:52
*描述：
*=====================================================================
*修改时间：2019/6/27 16:07:52
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Http2.Interfaces;
using SAEA.Http2.Model;
using System.Threading.Tasks;

namespace SAEA.Http2.Core
{
    /// <summary>
    /// 表示从http/1.1到http/2的升级请求的头和有效负载
    /// </summary>
    public class ClientUpgradeRequest
    {
        internal readonly Settings Settings;

        public string Base64EncodedSettings => base64Settings;

        private readonly string base64Settings;
        private readonly bool valid;
        internal TaskCompletionSource<IStream> UpgradeRequestStreamTcs
            = new TaskCompletionSource<IStream>();

        public bool IsValid => valid;

        public Task<IStream> UpgradeRequestStream => UpgradeRequestStreamTcs.Task;

        /// <summary>
        /// Constructs the ClientUpgradeRequest out ouf the information from
        /// the builder.
        /// </summary>
        internal ClientUpgradeRequest(
            Settings settings,
            string base64Settings,
            bool valid)
        {
            this.Settings = settings;
            this.base64Settings = base64Settings;
            this.valid = valid;
        }
    }
}
