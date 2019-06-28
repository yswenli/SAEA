/****************************************************************************
*项目名称：SAEA.Http2.Core
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Core
*类 名 称：Http1Response
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/28 17:09:21
*描述：
*=====================================================================
*修改时间：2019/6/28 17:09:21
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.Http2.Core
{
    class Http1Response
    {
        public string HttpVersion;
        public string StatusCode;
        public string Reason;

        private static Exception InvalidResponseHeaderException =
            new Exception("无效的响应头");

        public Dictionary<string, string> Headers = new Dictionary<string, string>();

        private static System.Text.RegularExpressions.Regex responseLineRegExp =
            new System.Text.RegularExpressions.Regex(
                @"([^\s]+) ([^\s]+) ([^\s]+)");

        public static Http1Response ParseFrom(string responseHeader)
        {
            var lines = responseHeader.Split(new string[] { "\r\n" }, StringSplitOptions.None);
            if (lines.Length < 1) throw InvalidResponseHeaderException;


            var match = responseLineRegExp.Match(lines[0]);
            if (!match.Success) throw InvalidResponseHeaderException;

            var httpVersion = match.Groups[1].Value;
            var statusCode = match.Groups[2].Value;
            var reason = match.Groups[3].Value;
            if (string.IsNullOrEmpty(httpVersion) ||
                string.IsNullOrEmpty(statusCode) ||
                string.IsNullOrEmpty(reason))
                throw InvalidResponseHeaderException;

            var headers = new Dictionary<string, string>();
            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                var colonIdx = line.IndexOf(':');
                if (colonIdx == -1) throw InvalidResponseHeaderException;
                var name = line.Substring(0, colonIdx).Trim().ToLowerInvariant();
                var value = line.Substring(colonIdx + 1).Trim();
                headers[name] = value;
            }

            return new Http1Response()
            {
                HttpVersion = httpVersion,
                StatusCode = statusCode,
                Reason = reason,
                Headers = headers,
            };
        }
    }
}
