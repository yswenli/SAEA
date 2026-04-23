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
*命名空间：SAEA.Common
*文件名： DNSHelper
*版本号： v26.4.23.1
*唯一标识：e019397a-3e92-4c12-9de6-29e447ee2a76
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/06/17 18:03:59
*描述：DNSHelper帮助类
*
*=====================================================================
*修改标记
*修改时间：2019/06/17 18:03:59
*修改人： yswenli
*版本号： v26.4.23.1
*描述：DNSHelper帮助类
*
*****************************************************************************/
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace SAEA.Common
{
    /// <summary>
    /// DNS辅助类
    /// </summary>
    public class DNSHelper
    {
        static readonly Random _rnd =new Random(Guid.NewGuid().GetHashCode());

        /// <summary>
        /// 通过uri获取ip port
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public static ValueTuple<string, int> GetIPPort(Uri uri)
        {
            ValueTuple<string, int> result = new ValueTuple<string, int>();

            DnsEndPoint dnsEndPoint = new DnsEndPoint(uri.Host, uri.Port);

            var ip = dnsEndPoint.AddressFamily.ToString();

            if (dnsEndPoint.AddressFamily == AddressFamily.Unspecified)
            {
                if (IPAddress.TryParse(uri.Host, out IPAddress address))
                {
                    ip = address.ToString();
                }
                else
                {
                    var list = Dns.GetHostAddresses(uri.Host);

                    if (list != null && list.Any())
                    {
                        ip = list.OrderBy(b => _rnd.Next(0, list.Length)).First().ToString();
                    }
                }
            }

            result.Item1 = ip;
            result.Item2 = dnsEndPoint.Port;
            return result;
        }
    }
}
