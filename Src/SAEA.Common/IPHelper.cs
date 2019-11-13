/****************************************************************************
*项目名称：SAEA.Common
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Common
*类 名 称：IPHelper
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/13 14:07:34
*描述：
*=====================================================================
*修改时间：2019/11/13 14:07:34
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;

namespace SAEA.Common
{
    public static class IPHelper
    {
        /// <summary>
        /// 是否是IP
        /// </summary>
        /// <param name="ip"></param>
        /// <returns></returns>
        public static bool IsIP(this string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return false;
            return Regex.IsMatch(ip, "^(([0 - 2]{ 0,1}[0-4]{0,1}\\d{0,1}?|25[0-5]?)\\.){3}([0 - 2]{0,1}[0-4]{0,1}\\d?|25[0-5]?)$");
        }

        public static bool IsPort(this string portStr, out ushort port)
        {
            if (ushort.TryParse(portStr, out port))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// ip转成long
        /// </summary>
        /// <param name="ip"></param>
        /// <returns></returns>
        public static long IPToNum(string ip)
        {
            char[] separator = new char[] { '.' };
            string[] items = ip.Split(separator);
            return long.Parse(items[0]) << 24
                    | long.Parse(items[1]) << 16
                    | long.Parse(items[2]) << 8
                    | long.Parse(items[3]);
        }

        /// <summary>
        /// long转成ip
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        public static string NumToIP(long num)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append((num >> 24) & 0xFF).Append(".");
            sb.Append((num >> 16) & 0xFF).Append(".");
            sb.Append((num >> 8) & 0xFF).Append(".");
            sb.Append(num & 0xFF);
            return sb.ToString();
        }
        /// <summary>
        /// 将port转换成字符串
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        public static string PortToString(this ushort port)
        {
            var bs = BitConverter.GetBytes(port).Reverse().ToArray();
            return string.Join(",", bs);
        }
        /// <summary>
        /// 将字符串转换成port
        /// </summary>
        /// <param name="portStr"></param>
        /// <returns></returns>
        public static int StringToPort(this string portStr)
        {
            var arr = portStr.Split(",");
            var num1 = int.Parse(arr[0]);
            var num2 = int.Parse(arr[1]);
            return (num1 << 8) + num2;
        }

        /// <summary>
        /// 获取正在使用中的端口
        /// </summary>
        /// <param name="address">指定IP地址,默认全部地址</param>
        /// <returns></returns>
        public static int[] GetInUsedPort(string address = null)
        {
            List<IPEndPoint> localEP = new List<IPEndPoint>();
            List<int> localPort = new List<int>();
            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            localEP.AddRange(ipGlobalProperties.GetActiveTcpListeners());
            localEP.AddRange(ipGlobalProperties.GetActiveUdpListeners());
            localEP.AddRange(ipGlobalProperties.GetActiveTcpConnections().Select(item => item.LocalEndPoint));
            foreach (var item in localEP.Distinct())
            {
                if (address == null || item.Address.ToString() == address)
                    localPort.Add(item.Port);
            }
            localPort.Sort();
            return localPort.Distinct().ToArray();
        }

        /// <summary>
        /// 随机获取一个大于等于 min 的空闲端口
        /// </summary>
        /// <param name="min">指定起始端口，默认1024</param>
        /// <param name="address">指定IP地址,默认全部地址</param>
        /// <returns></returns>
        public static ushort GetFreePort(int min = 1024, string address = null)
        {
            ushort freePort = 39654;
            Random random = new Random();
            int[] freePorts = GetInUsedPort(address)
                .Where(x => x >= (min = min <= 0 ? 1 : min))
                .ToArray();
            while (freePort < 0)
            {
                freePort = (ushort)random.Next(min, 65536);
                foreach (var item in freePorts)
                {
                    if (freePort == item)
                        freePort = 39654;
                }
            }
            return freePort;
        }


    }
}
