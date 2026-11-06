/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：System
*文件名： StringHelper
*版本号： v26.4.23.1
*唯一标识：90ac9fbf-2109-472d-9d61-a267f88d8a3e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/08/24 16:31:11
*描述：StringHelper接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:45
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Common;

using System.Collections.Generic;
using System.Net;
using System.Text;

namespace System
{
    /// <summary>
    /// 字符串扩展工具类，含容量单位格式化、拆分、编解码等辅助方法
    /// </summary>
    public static class StringHelper
    {
        static int k = 1024;

        static int m = k * k;

        static long g = m * k;

        static long t = g * k;

        /// <summary>
        /// 将字节数按 1024 进制格式化为 B、KB、MB、GB 字符串，保留两位小数
        /// </summary>
        /// <param name="len">字节数</param>
        /// <returns>带单位的可读字符串</returns>
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

        /// <summary>
        /// 将字节数格式化为带单位的可读字符串
        /// </summary>
        /// <param name="l">字节数</param>
        /// <returns>带单位的可读字符串</returns>
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
            if (string.IsNullOrEmpty(ipStr)) return new ValueTuple<string, int>("", 0);
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

        /// <summary>
        /// 将 ip:port 字符串转为 IPEndPoint，格式错误时抛出异常
        /// </summary>
        /// <param name="remote">地址字符串</param>
        /// <returns>地址端点</returns>
        public static IPEndPoint ToIPEndPoint(this string remote)
        {
            var tuple = remote.ToIPPort();
            return new IPEndPoint(IPAddress.Parse(tuple.Item1), tuple.Item2);
        }


        /// <summary>
        /// 按多个分隔符拆分字符串
        /// </summary>
        /// <param name="str">源字符串</param>
        /// <param name="none">为 true 时保留空项，否则移除空项</param>
        /// <param name="splits">分隔符集合</param>
        /// <returns>拆分结果</returns>
        public static string[] ToArray(this string str, bool none = false, params string[] splits)
        {
            return str.Split(splits, none ? StringSplitOptions.None : StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// 按单个字符串分隔符拆分，仅供测试使用
        /// </summary>
        /// <param name="str">源字符串</param>
        /// <param name="splitStr">分隔符</param>
        /// <param name="option">是否移除空项</param>
        /// <returns>拆分结果</returns>
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


        /// <summary>
        /// 按字符串分隔符拆分，未命中分隔符时返回仅含原字符串的数组
        /// </summary>
        /// <param name="str">源字符串</param>
        /// <param name="splitStr">分隔符</param>
        /// <returns>拆分结果</returns>
        public static string[] Split(this string str, string splitStr)
        {
            return str.Split(splitStr, StringSplitOptions.None);
        }


        /// <summary>
        /// 截取指定起始位置与长度的子字符串
        /// </summary>
        /// <param name="str">源字符串</param>
        /// <param name="start">起始索引</param>
        /// <param name="length">截取长度</param>
        /// <returns>子字符串</returns>
        public static string Substring(this string str, int start, int length)
        {
            return str.AsSpan(start, length).ToString();
        }

        /// <summary>
        /// 从指定位置截取到字符串末尾
        /// </summary>
        /// <param name="str">源字符串</param>
        /// <param name="start">起始索引</param>
        /// <returns>子字符串</returns>
        public static string Substring(this string str, int start)
        {
            return str.AsSpan(start, str.Length - start).ToString();
        }

        /// <summary>
        /// 截取子串并解析为整数，格式不合法时抛出异常
        /// </summary>
        /// <param name="str">源字符串</param>
        /// <param name="start">起始索引</param>
        /// <param name="count">截取长度</param>
        /// <returns>解析得到的整数</returns>
        public static int ParseToInt(this string str, int start, int count)
        {
            return int.Parse(Substring(str, start, count));
        }

        /// <summary>
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
        /// <summary>
        /// 对字符串做 URL 编码
        /// </summary>
        /// <param name="str">源字符串</param>
        /// <returns>编码后的字符串</returns>
        public static string UrlEncode(this string str)
        {
            return System.Web.HttpUtility.UrlEncode(str);
        }

        /// <summary>
        /// 对字符串做 URL 解码
        /// </summary>
        /// <param name="str">源字符串</param>
        /// <returns>解码后的字符串</returns>
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

        /// <summary>
        /// 将字符串转为base64字符串
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string ToBase64String(this string str)
        {
            return System.Convert.ToBase64String(Encoding.UTF8.GetBytes(str));
        }

        /// <summary>
        /// 将base64字符串转为字符串
        /// </summary>
        /// <param name="base64Str"></param>
        /// <returns></returns>
        public static string FromBase64String(this string base64Str)
        {
            return Encoding.UTF8.GetString(System.Convert.FromBase64String(base64Str));
        }


        /// <summary>
        /// 判断字符串是否为 null 或空字符串
        /// </summary>
        /// <param name="str">待判断字符串</param>
        /// <returns>为 null 或空返回 true</returns>
        public static bool IsNullOrEmpty(this string str)
        {
            return string.IsNullOrEmpty(str);
        }


        /// <summary>
        /// 判断字符串是否非 null 且非空
        /// </summary>
        /// <param name="str">待判断字符串</param>
        /// <returns>非 null 且非空返回 true</returns>
        public static bool IsNotNullOrEmpty(this string str)
        {
            return !string.IsNullOrEmpty(str);
        }


    }
}