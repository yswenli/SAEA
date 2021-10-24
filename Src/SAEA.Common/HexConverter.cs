/****************************************************************************
*项目名称：SAEA.Common
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Common
*类 名 称：HexConverter
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/30 19:17:09
*描述：
*=====================================================================
*修改时间：2020/12/30 19:17:09
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SAEA.Common
{
    /// <summary>
    /// 进制转换器
    /// </summary>
    public class HexConverter
    {
        /// <summary>
        /// 进制格式字符
        /// </summary>
        public string FormatString
        {
            get;
            private set;
        }
        /// <summary>
        /// 格式字符长度
        /// </summary>
        public int Length
        {
            get
            {
                if (FormatString != null)
                    return FormatString.Length;
                else
                    return 0;
            }

        }
        /// <summary>
        /// 进制转换器
        /// </summary>
        /// <param name="formatStr"></param>
        public HexConverter(string formatStr = "01")
        {
            FormatString = formatStr;
        }

        /// <summary>
        /// 数字转换为指定的进制形式字符串，
        /// 倍数就是位数，
        /// 模数就是具体显示内容
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        public string ToString(long number)
        {
            if (number == 0) return number.ToString();
            List<string> result = new List<string>();
            long t = number;
            do
            {
                var mod = t % Length;
                t = Math.Abs(t / Length);
                var character = FormatString[Convert.ToInt32(mod)].ToString();
                result.Insert(0, character);
            }
            while (t > 0);
            return string.Join("", result.ToArray());
        }

        /// <summary>
        /// 指定字符串转换为指定进制的数字形式，
        /// 显示内容的位置就是模，
        /// 位数就倍数
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public long FromString(string str)
        {
            long result = 0;
            int j = 0;
            foreach (var ch in str.ToCharArray().Reverse().ToArray())
            {
                if (FormatString.Contains(ch))
                {
                    result += FormatString.IndexOf(ch) * ((long)Math.Pow(Length, j));
                    j++;
                }
            }
            return result;
        }
        /// <summary>
        /// 将数字转换成指定格式进制
        /// </summary>
        /// <param name="number"></param>
        /// <param name="formatStr"></param>
        /// <returns></returns>
        public static string ToString(long number, string formatStr)
        {
            var num = new HexConverter(formatStr);
            return num.ToString(number);
        }
        /// <summary>
        /// 将数字转换成指定进制
        /// </summary>
        /// <param name="number"></param>
        /// <param name="hexSize"></param>
        /// <returns></returns>
        public static string ToString(long number, int hexSize = 2)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hexSize; i++)
            {
                if (i > 61)
                {
                    sb.Append(Convert.ToChar(65 + i - 62));
                }
                if (i > 35)
                {
                    sb.Append(Convert.ToChar(65 + i - 36));
                }
                else if (i > 9)
                {
                    sb.Append(Convert.ToChar(87 + i));
                }
                else
                    sb.Append(i);
            }
            return ToString(number, sb.ToString());
        }

        /// <summary>
        /// 将指定字符从指定格式进制还原成数字
        /// </summary>
        /// <param name="str"></param>
        /// <param name="formatStr"></param>
        /// <returns></returns>
        public static long FromString(string str, string formatStr)
        {
            var num = new HexConverter(formatStr);
            return num.FromString(str);
        }
        /// <summary>
        /// 将指定字符从指定进制还原成数字
        /// </summary>
        /// <param name="str"></param>
        /// <param name="hexSize"></param>
        /// <returns></returns>
        public static long FromString(string str, int hexSize = 2)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hexSize; i++)
            {
                if (i > 61)
                {
                    sb.Append(Convert.ToChar(65 + i - 62));
                }
                if (i > 35)
                {
                    sb.Append(Convert.ToChar(65 + i - 36));
                }
                else if (i > 9)
                {
                    sb.Append(Convert.ToChar(87 + i));
                }
                else
                    sb.Append(i);
            }
            return FromString(str, sb.ToString());
        }
    }
}
