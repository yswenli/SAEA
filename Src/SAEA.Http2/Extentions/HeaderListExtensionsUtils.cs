/****************************************************************************
*项目名称：SAEA.Http2.Extentions
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Extentions
*类 名 称：HeaderListExtensionsUtils
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 16:13:24
*描述：
*=====================================================================
*修改时间：2019/6/27 16:13:24
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Http2.Model;
using System.Collections.Generic;
using System.Globalization;

namespace SAEA.Http2.Extentions
{
    /// <summary>
    /// 使用标题列表的实用方法
    /// </summary>
    internal static class HeaderListExtensionsUtils
    {
        public static long GetContentLength(this IEnumerable<HeaderField> headerFields)
        {
            foreach (var hf in headerFields)
            {
                if (hf.Name == "content-length")
                {
                    long result;
                    if (!long.TryParse(
                        hf.Value, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out result) || result < 0)
                    {
                        return -2;
                    }
                    return result;
                }
            }

            return -1;
        }

        public static bool IsInformationalHeaders(
            this IEnumerable<HeaderField> headerFields)
        {
            foreach (var hf in headerFields)
            {
                if (hf.Name == ":status")
                {
                    var statusValue = hf.Value;
                    if (statusValue.Length != 3) return false;
                    return
                        statusValue[0] == '1' &&
                        statusValue[1] >= '0' && statusValue[1] <= '9' &&
                        statusValue[2] >= '0' && statusValue[2] <= '9';
                }
                else if (hf.Name.Length > 0 && hf.Name[0] != ':')
                {
                    return false;
                }
            }

            return false;
        }
    }
}
