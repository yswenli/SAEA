/****************************************************************************
*项目名称：SAEA.Mongo.Driver
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Mongo.Driver
*类 名 称：ApplicationNameHelper
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/5/24 11:17:13
*描述：
*=====================================================================
*修改时间：2019/5/24 11:17:13
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Mongo.Bson.IO;
using System;

namespace SAEA.Mongo.Driver
{
    internal static class ApplicationNameHelper
    {
        public static string EnsureApplicationNameIsValid(string applicationName, string paramName)
        {
            string message;
            if (!IsApplicationNameValid(applicationName, out message))
            {
                throw new ArgumentException(message, paramName);
            }

            return applicationName;
        }

        public static bool IsApplicationNameValid(string applicationName, out string message)
        {
            if (applicationName != null)
            {
                var utf8 = Utf8Encodings.Strict.GetBytes(applicationName);
                if (utf8.Length > 128)
                {
                    message = "Application name exceeds 128 bytes after encoding to UTF8.";
                    return false;
                }
            }

            message = null;
            return true;
        }
    }
}
