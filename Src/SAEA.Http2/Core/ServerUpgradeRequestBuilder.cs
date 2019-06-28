/****************************************************************************
*项目名称：SAEA.Http2.Core
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Core
*类 名 称：ServerUpgradeRequestBuilder
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 16:08:51
*描述：
*=====================================================================
*修改时间：2019/6/27 16:08:51
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Http2.Extentions;
using SAEA.Http2.Model;
using System;
using System.Collections.Generic;

namespace SAEA.Http2.Core
{
    public class ServerUpgradeRequestBuilder
    {
        Settings? settings;
        List<HeaderField> headers;
        ArraySegment<byte> payload;

        /// <summary>
        ///创建新的ServerUpgradeRequestBuilder
        /// </summary>
        public ServerUpgradeRequestBuilder()
        {
        }

        /// <summary>
        /// ServerUpgradeRequest
        /// </summary>
        /// <returns></returns>
        public ServerUpgradeRequest Build()
        {
            bool valid = true;
            if (settings == null) valid = false;

            var headers = this.headers;
            if (headers == null) valid = false;
            else
            {
                var hvr = HeaderValidator.ValidateRequestHeaders(headers);
                if (hvr != HeaderValidationResult.Ok)
                {
                    headers = null;
                    valid = false;
                }
            }

            long declaredContentLength = -1;
            if (headers != null)
            {
                declaredContentLength = headers.GetContentLength();
            }

            byte[] payload = null;
            if (this.payload != null && this.payload.Count > 0)
            {
                if (declaredContentLength != this.payload.Count)
                {
                    valid = false;
                }
                else
                {
                    payload = new byte[this.payload.Count];
                    Array.Copy(
                        this.payload.Array, this.payload.Offset,
                        payload, 0,
                        this.payload.Count);
                }
            }
            else if (declaredContentLength > 0)
            {
                valid = false;
            }

            return new ServerUpgradeRequest(
                settings: settings ?? Settings.Default,
                headers: headers,
                payload: payload,
                valid: valid);
        }

        public ServerUpgradeRequestBuilder SetHeaders(List<HeaderField> headers)
        {
            this.headers = headers;
            return this;
        }


        public ServerUpgradeRequestBuilder SetHttp2Settings(
            string base64EncodedHttp2Settings)
        {
            settings = null;

            var chars = base64EncodedHttp2Settings.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (chars[i] == '_') chars[i] = '/';
                else if (chars[i] == '-') chars[i] = '+';
            }


            byte[] settingsBytes;
            try
            {
                settingsBytes =
                    Convert.FromBase64CharArray(chars, 0, chars.Length);
            }
            catch (Exception)
            {
                return this;
            }


            Settings newSettings = Settings.Default;
            var err = newSettings.UpdateFromData(
                new ArraySegment<byte>(settingsBytes));
            if (err == null)
            {
                settings = newSettings;
            }

            return this;
        }


        public ServerUpgradeRequestBuilder SetPayload(
            ArraySegment<byte> payload)
        {
            this.payload = payload;
            return this;
        }
    }
}
