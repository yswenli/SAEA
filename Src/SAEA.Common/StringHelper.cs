/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Commom
*文件名： Class1
*版本号： V2.2.2.1
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： V2.2.2.1
*描述：
*
*****************************************************************************/

using System;

namespace SAEA.Common
{
    public static class StringHelper
    {
        public const string ASTERRISK = "*";

        public const string MINUSSIGN = "-";

        public const string DOLLAR = "$";

        public const string COLON = ":";

        public const string SLASH = "/";

        public const string LESS_THAN = "<";

        public const string GREATER_THAN = ">";




        static int k = 1024;

        static int m = k * k;

        static long g = m * k;

        static long t = g * k;

        public static string Convert(long len)
        {
            string result = string.Empty;

            if (len < k)
            {
                result = string.Format("{0:F} B", len);
            }
            else if (len < m)
            {
                result = string.Format("{0} KB", ((len / 1.00 / k)).ToString("f2"));
            }
            else if (len < g)
            {
                result = string.Format("{0} MB", ((len / 1.00 / m)).ToString("f2"));
            }
            else
            {
                result = string.Format("{0} GB", ((len / 1.00 / g)).ToString("f2"));
            }
            return result;
        }

        public static string ToSpeedString(this long l)
        {
            return Convert(l);
        }

        public static Tuple<string, int> GetIPPort(this string remote)
        {
            Tuple<string, int> result;

            var arr = remote.Split(new string[] { ":", " ", "," }, StringSplitOptions.None);

            var ip = arr[0];

            var port = int.Parse(arr[1]);

            result = new Tuple<string, int>(ip, port);

            return result;
        }

        public static string[] ToArray(this string str, bool none = false, params string[] splits)
        {
            return str.Split(splits, none ? StringSplitOptions.None : StringSplitOptions.RemoveEmptyEntries);
        }

        public static string[] Split(this string str, string splitStr, StringSplitOptions option)
        {
            return str.Split(new string[] { splitStr }, option);
        }

        public static string[] Split(this string str, string splitStr)
        {
            return str.Split(splitStr, StringSplitOptions.None);
        }



        public static string SSubstring(this string str, int start, int length)
        {
            ReadOnlySpan<char> readOnlySpan = str.AsSpan(start, length);
            return readOnlySpan.ToString();
        }

        public static string SSubstring(this string str, int start)
        {
            ReadOnlySpan<char> readOnlySpan = str.AsSpan(start, str.Length - start);
            return readOnlySpan.ToString();
        }


        public static int ParseToInt(this string str, int start, int length)
        {
            int result = 0;

            ReadOnlySpan<char> readOnlySpan = str.AsSpan(start, length);

            var sign = 1;
            var index = 0;

            if (readOnlySpan[0].Equals('-'))
            {
                sign = -1;
                index = 1;
            }
            for (int i = index; i < length; i++)
            {
                var cint = System.Convert.ToInt16(readOnlySpan[i]);
                if (cint >= 48 && cint <= 57)
                    result += (cint - 48) * (int)Math.Pow(10, length - i - 1);
            }
            return result * sign;
        }


        public static long ParseToLong(this string str, int start, int length)
        {
            long result = 0;

            ReadOnlySpan<char> readOnlySpan = str.AsSpan(start, length);

            var sign = 1;
            var index = 0;

            if (readOnlySpan[0].Equals('-'))
            {
                sign = -1;
                index = 1;
            }
            for (int i = index; i < length; i++)
            {
                var cint = System.Convert.ToInt16(readOnlySpan[i]);
                if (cint >= 48 && cint <= 57)
                    result += (cint - 48) * (long)Math.Pow(10, length - i - 1);
            }
            return result * sign;
        }
    }
}
