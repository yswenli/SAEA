/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Commom
*文件名： Class1
*版本号： v4.2.3.1
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
*版本号： v4.2.3.1
*描述：
*
*****************************************************************************/

using System;

namespace SAEA.Common
{
    public static class StringHelper
    {
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

        /// <summary>
        /// 自定义扩展
        /// </summary>
        /// <param name="str"></param>
        /// <param name="splitStr"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static string[] Split(this string str, string splitStr, StringSplitOptions option = StringSplitOptions.None)
        {
            return str.Split(new string[] { splitStr }, option);
        }

    }
}
