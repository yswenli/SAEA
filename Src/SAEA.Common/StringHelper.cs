/****************************************************************************
*Copyright (c) 2018-2021yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Commom
*文件名： Class1
*版本号： v7.0.0.1
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
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

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

        /// <summary>
        /// 字符串拆分成ip和port
        /// </summary>
        /// <param name="ipStr"></param>
        /// <returns></returns>
        public static ValueTuple<string, int> ToIPPort(this string ipStr)
        {
            try
            {
                ValueTuple<string, int> result;

                var arr = ipStr.Split(new string[] { ConstHelper.COLON, ConstHelper.SPACE, ConstHelper.COMMA }, StringSplitOptions.None);

                if (string.IsNullOrEmpty(arr[0])) arr[0] = "127.0.0.1";

                var ip = arr[0];

                if (string.IsNullOrEmpty(arr[1]))
                {
                    throw new Exception("port:" + arr[1]);
                }

                var ai = arr[1].IndexOf("@");

                if (ai > -1)
                {
                    arr[1] = arr[1].Substring(0, ai);
                }

                var port = int.Parse(arr[1]);

                result = new ValueTuple<string, int>(ip, port);

                return result;
            }
            catch (Exception ex)
            {
                throw new Exception("转换成IPPort失败，ipStr 内容格式不正确，ipStr：" + ipStr, ex);
            }
        }

        public static IPEndPoint ToIPEndPoint(this string remote)
        {
            var tuple = remote.ToIPPort();
            return new IPEndPoint(IPAddress.Parse(tuple.Item1), tuple.Item2);
        }


        public static string[] ToArray(this string str, bool none = false, params string[] splits)
        {
            return str.Split(splits, none ? StringSplitOptions.None : StringSplitOptions.RemoveEmptyEntries);
        }

        [Obsolete("测试用")]
        public static string[] Split2(this string str, string splitStr, StringSplitOptions option)
        {
            return str.Split(new string[] { splitStr }, option);
        }

        /// <summary>
        /// 自定义分隔
        /// </summary>
        /// <param name="str"></param>
        /// <param name="splitStr"></param>
        /// <param name="option"></param>
        /// <returns></returns>
        public static string[] Split(this string str, string splitStr, StringSplitOptions option)
        {
            if (string.IsNullOrEmpty(str) || string.IsNullOrEmpty(splitStr) || str.IndexOf(splitStr) == -1) return new string[] { str };

            var strSpan = str.AsSpan();

            var splitSapn = splitStr.AsSpan();

            int m = 0, n = 0;

            List<string> arr = new List<string>();

            while (true)
            {
                m = n;
                n = strSpan.IndexOf(splitSapn);
                if (n > -1)
                {
                    arr.Add(strSpan.Slice(0, n).ToString());
                    strSpan = strSpan.Slice(n + splitSapn.Length);
                }
                else
                {
                    arr.Add(strSpan.Slice(0).ToString());
                    break;
                }
            }
            if (option == StringSplitOptions.RemoveEmptyEntries)
            {
                arr.RemoveAll(b => string.IsNullOrEmpty(b));
            }
            return arr.ToArray();
        }


        public static string[] Split(this string str, string splitStr)
        {
            return str.Split(splitStr, StringSplitOptions.None);
        }


        public static string Substring(this string str, int start, int length)
        {
            return str.AsSpan(start, length).ToString();
        }

        public static string Substring(this string str, int start)
        {
            return str.AsSpan(start, str.Length - start).ToString();
        }

        public static int ParseToInt(this string str, int start, int count)
        {
            return int.Parse(Substring(str, start, count));
        }

        // <summary>
        /// byte[]转为16进制字符串
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string ByteToHexStr(this byte[] bytes)
        {
            string returnStr = "";
            if (bytes != null)
            {
                for (int i = 0; i < bytes.Length; i++)
                {
                    returnStr += bytes[i].ToString("X2");
                }
            }
            return returnStr;
        }

        /// <summary>
        /// 将16进制的字符串转为byte[]
        /// </summary>
        /// <param name="hexString"></param>
        /// <returns></returns>
        public static byte[] StrToHexByte(this string hexString)
        {
            hexString = hexString.Replace(" ", "");
            if ((hexString.Length % 2) != 0)
                hexString += " ";
            byte[] returnBytes = new byte[hexString.Length / 2];
            for (int i = 0; i < returnBytes.Length; i++)
                returnBytes[i] = System.Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            return returnBytes;
        }


        /// <summary>
        /// 将16进制字符串转为字符串
        /// </summary>
        /// <param name="hs"></param>
        /// <param name="encode"></param>
        /// <returns></returns>
        public static string HexStringToString(this string hs, Encoding encode)
        {
            string strTemp = "";
            byte[] b = new byte[hs.Length / 2];
            for (int i = 0; i < hs.Length / 2; i++)
            {
                strTemp = hs.Substring(i * 2, 2);
                b[i] = System.Convert.ToByte(strTemp, 16);
            }
            //按照指定编码将字节数组变为字符串
            return encode.GetString(b);
        }
        /// <summary>
        /// 将字符串转为16进制字符，允许中文
        /// </summary>
        /// <param name="s"></param>
        /// <param name="encode"></param>
        /// <param name="spanString"></param>
        /// <returns></returns>
        public static string StringToHexString(this string s, Encoding encode, string spanString)
        {
            byte[] b = encode.GetBytes(s);//按照指定编码将string编程字节数组
            string result = string.Empty;
            for (int i = 0; i < b.Length; i++)//逐字节变为16进制字符
            {
                result += System.Convert.ToString(b[i], 16) + spanString;
            }
            return result;
        }


        /// <summary>
        /// url encode
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string UrlEncode(this string str)
        {
            return System.Web.HttpUtility.UrlEncode(str);
        }

        /// <summary>
        /// url decode
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string UrlDecode(this string str)
        {
            return System.Web.HttpUtility.UrlDecode(str);
        }


        /// <summary>
        /// 计算含有emoji表情字符串的长度
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static int GetStringLength(this string str)
        {
            int len = 0;

            if (!string.IsNullOrEmpty(str))
            {
                var bytes = Encoding.UTF8.GetBytes(str);

                var fstr = str;

                //System.IO.File.AppendAllText();

                int i = 0;

                while (i < bytes.Length)
                {
                    var k = bytes[i];

                    if (k <= 127)
                    {
                        i += 1;
                    }
                    else if (k < 224)
                    {
                        i += 2;
                    }
                    else if (k < 240)
                    {
                        i += 3;
                    }
                    else
                    {
                        i += 4;
                    }
                    len++;
                }

            }

            return len;
        }
    }
}
