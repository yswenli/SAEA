/****************************************************************************
*项目名称：SAEA.Common
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Common
*类 名 称：DNSHelper
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/17 17:17:15
*描述：
*=====================================================================
*修改时间：2019/6/17 17:17:15
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
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
