/****************************************************************************
*项目名称：SAEA.Http2.Core
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Core
*类 名 称：ClientUpgradeRequestBuilder
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/28 16:21:48
*描述：
*=====================================================================
*修改时间：2019/6/28 16:21:48
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Http2.Model;
using System;

namespace SAEA.Http2.Core
{
    /// <summary>
    /// 用于客户端升级请求的生成器
    /// </summary>
    public class ClientUpgradeRequestBuilder
    {
        Settings settings = Settings.Default;

        public ClientUpgradeRequestBuilder()
        {
            settings.EnablePush = false; // Not available
        }

        /// <summary>
        /// 从存储的配置生成ClientUpgradeRequest
        /// </summary>
        /// <returns></returns>
        public ClientUpgradeRequest Build()
        {
            return new ClientUpgradeRequest(
                settings: settings,
                base64Settings: SettingsToBase64String(settings),
                valid: true);
        }

        /// <summary>
        /// 设置HTTP/2设置，该设置将采用base64编码，并且必须与http2设置头中的升级请求一起发送。.
        /// </summary>
        public ClientUpgradeRequestBuilder SetHttp2Settings(Settings settings)
        {
            if (!settings.Valid)
                throw new ArgumentException(
                    "不能使用无效设置", nameof(settings));

            settings.EnablePush = false;
            this.settings = settings;
            return this;
        }

        /// <summary>
        /// 将设置编码为base64字符串
        /// </summary>
        private string SettingsToBase64String(Settings settings)
        {
            var settingsAsBytes = new byte[settings.RequiredSize];
            settings.EncodeInto(new ArraySegment<byte>(settingsAsBytes));
            var encodeBuf = new char[2 * settings.RequiredSize];
            var encodedLength = Convert.ToBase64CharArray(
                settingsAsBytes, 0, settingsAsBytes.Length,
                encodeBuf, 0);

            for (var i = 0; i < encodedLength; i++)
            {
                if (encodeBuf[i] == '/') encodeBuf[i] = '_';
                else if (encodeBuf[i] == '+') encodeBuf[i] = '-';
            }

            return new string(encodeBuf, 0, encodedLength);
        }
    }
}
