/****************************************************************************
*项目名称：SAEA.Mongo.Bson
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Mongo.Bson
*类 名 称：HexUtils
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/5/22 11:11:01
*描述：
*=====================================================================
*修改时间：2019/5/22 11:11:01
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SAEA.Mongo.Bson
{
    internal static class HexUtils
    {
        public static bool IsValidHexDigit(char c)
        {
            return
                c >= '0' && c <= '9' ||
                c >= 'a' && c <= 'f' ||
                c >= 'A' && c <= 'F';
        }

        public static bool IsValidHexString(string s)
        {
            for (var i = 0; i < s.Length; i++)
            {
                if (!IsValidHexDigit(s[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public static int ParseInt32(string value)
        {
            return int.Parse(value, NumberStyles.HexNumber);
        }
    }
}
