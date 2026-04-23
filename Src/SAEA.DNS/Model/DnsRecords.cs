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
*命名空间：SAEA.DNS.Model
*文件名： DnsRecords
*版本号： v26.4.23.1
*唯一标识：e9a6fce9-ab87-46a4-92a2-760357f50a99
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 22:58:12
*描述：
*
*=====================================================================
*修改标记
*修改时间：2019/11/29 22:58:12
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using SAEA.DNS.Coder;
using SAEA.DNS.Common.ResourceRecords;
using SAEA.DNS.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.DNS.Model
{
    /// <summary>
    /// DNSDataFile
    /// </summary>
    public class DnsRecords : IRequestCoder
    {
        private static readonly TimeSpan DEFAULT_TTL = new TimeSpan(0);

        private static bool Matches(Domain domain, Domain entry)
        {
            string[] labels = entry.ToString().Split('.');
            string[] patterns = new string[labels.Length];

            for (int i = 0; i < labels.Length; i++)
            {
                string label = labels[i];
                patterns[i] = label == "*" ? "(\\w+)" : Regex.Escape(label);
            }

            Regex re = new Regex("^" + string.Join("\\.", patterns) + "$");
            return re.IsMatch(domain.ToString());
        }

        private static void Merge<T>(IList<T> l1, IList<T> l2)
        {
            foreach (T obj in l2)
            {
                l1.Add(obj);
            }
        }

        private IList<IResourceRecord> entries = new List<IResourceRecord>();

        private TimeSpan ttl = DEFAULT_TTL;

        public DnsRecords(TimeSpan ttl)
        {
            this.ttl = ttl;
        }

        public DnsRecords() { }

        public void Add(IResourceRecord entry)
        {
            entries.Add(entry);
        }

        /// <summary>
        /// 添加域名ip映射
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="ip"></param>
        public void AddIPAddressResourceRecord(string domain, string ip)
        {
            AddIPAddressResourceRecord(new Domain(domain), IPAddress.Parse(ip));
        }

        /// <summary>
        /// 添加域名ip映射
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="ip"></param>
        public void AddIPAddressResourceRecord(Domain domain, IPAddress ip)
        {
            Add(new IPAddressResourceRecord(domain, ip, ttl));
        }

        /// <summary>
        /// 添加名称服务器资源记录
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="nsDomain"></param>
        public void AddNameServerResourceRecord(string domain, string nsDomain)
        {
            AddNameServerResourceRecord(new Domain(domain), new Domain(nsDomain));
        }

        /// <summary>
        /// 添加名称服务器资源记录
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="nsDomain"></param>
        public void AddNameServerResourceRecord(Domain domain, Domain nsDomain)
        {
            Add(new NameServerResourceRecord(domain, nsDomain, ttl));
        }

        /// <summary>
        /// 添加规范名称资源记录
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="cname"></param>
        public void AddCanonicalNameResourceRecord(string domain, string cname)
        {
            AddCanonicalNameResourceRecord(new Domain(domain), new Domain(cname));
        }

        /// <summary>
        /// 添加规范名称资源记录
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="cname"></param>
        public void AddCanonicalNameResourceRecord(Domain domain, Domain cname)
        {
            Add(new CanonicalNameResourceRecord(domain, cname, ttl));
        }

        /// <summary>
        /// 添加指针资源记录
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="pointer"></param>
        public void AddPointerResourceRecord(string ip, string pointer)
        {
            AddPointerResourceRecord(IPAddress.Parse(ip), new Domain(pointer));
        }
        /// <summary>
        /// 添加指针资源记录
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="pointer"></param>
        public void AddPointerResourceRecord(IPAddress ip, Domain pointer)
        {
            Add(new PointerResourceRecord(ip, pointer, ttl));
        }

        /// <summary>
        /// 添加邮件交换资源记录
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="preference"></param>
        /// <param name="exchange"></param>
        public void AddMailExchangeResourceRecord(string domain, int preference, string exchange)
        {
            AddMailExchangeResourceRecord(new Domain(domain), preference, new Domain(exchange));
        }

        /// <summary>
        /// 添加邮件交换资源记录
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="preference"></param>
        /// <param name="exchange"></param>
        public void AddMailExchangeResourceRecord(Domain domain, int preference, Domain exchange)
        {
            Add(new MailExchangeResourceRecord(domain, preference, exchange));
        }

        /// <summary>
        /// 添加文本资源记录
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="attributeName"></param>
        /// <param name="attributeValue"></param>
        public void AddTextResourceRecord(string domain, string attributeName, string attributeValue)
        {
            Add(new TextResourceRecord(new Domain(domain), attributeName, attributeValue, ttl));
        }

        /// <summary>
        /// 解析
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<IResponse> Code(IRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            IResponse response = Protocol.DnsResponseMessage.FromRequest(request);

            foreach (Question question in request.Questions)
            {
                IList<IResourceRecord> answers = Get(question);

                if (answers.Count > 0)
                {
                    Merge(response.AnswerRecords, answers);
                }
                else
                {
                    response.ResponseCode = ResponseCode.NameError;
                }
            }

            return Task.FromResult(response);
        }

        private IList<IResourceRecord> Get(Domain domain, RecordType type)
        {
            return entries.Where(e => Matches(domain, e.Name) && e.Type == type).ToList();
        }

        private IList<IResourceRecord> Get(Question question)
        {
            return Get(question.Name, question.Type);
        }
    }
}
