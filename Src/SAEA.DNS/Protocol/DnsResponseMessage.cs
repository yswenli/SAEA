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
*命名空间：SAEA.DNS.Protocol
*文件名： DnsResponseMessage
*版本号： v26.4.23.1
*唯一标识：04d1f9b6-c0c2-4c0c-9345-3f32cd3c9498
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 22:58:12
*描述：DnsResponseMessage消息类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:47
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.DNS.Common.ResourceRecords;
using SAEA.DNS.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAEA.DNS.Protocol
{
    /// <summary>
    /// Dns回复数据消息体
    /// </summary>
    public class DnsResponseMessage : IResponse
    {
        private static readonly Random RANDOM = new Random();

        private Header header;
        private IList<Question> questions;
        private IList<IResourceRecord> answers;
        private IList<IResourceRecord> authority;
        private IList<IResourceRecord> additional;

        /// <summary>
        /// 以请求为基础创建响应，沿用请求标识并复制问题列表
        /// </summary>
        /// <param name="request">对应的请求</param>
        /// <returns>新建的响应消息</returns>
        public static DnsResponseMessage FromRequest(IRequest request)
        {
            DnsResponseMessage response = new DnsResponseMessage();

            response.Id = request.Id;

            foreach (Question question in request.Questions)
            {
                response.Questions.Add(question);
            }

            return response;
        }

        /// <summary>
        /// 从字节数组解析响应消息，截断的响应只解析问题段
        /// </summary>
        /// <param name="message">响应消息字节</param>
        /// <returns>解析后的响应消息</returns>
        public static DnsResponseMessage FromArray(byte[] message)
        {
            Header header = Header.FromArray(message);
            int offset = header.Size;

            if (!header.Response || header.QuestionCount == 0)
            {
                throw new ArgumentException("Invalid response message");
            }

            if (header.Truncated)
            {
                return new DnsResponseMessage(header,
                    Question.GetAllFromArray(message, offset, header.QuestionCount),
                    new List<IResourceRecord>(),
                    new List<IResourceRecord>(),
                    new List<IResourceRecord>());
            }

            return new DnsResponseMessage(header,
                Question.GetAllFromArray(message, offset, header.QuestionCount, out offset),
                ResourceRecordFactory.GetAllFromArray(message, offset, header.AnswerRecordCount, out offset),
                ResourceRecordFactory.GetAllFromArray(message, offset, header.AuthorityRecordCount, out offset),
                ResourceRecordFactory.GetAllFromArray(message, offset, header.AdditionalRecordCount, out offset));
        }

        /// <summary>
        /// Dns回复数据消息体
        /// </summary>
        /// <param name="header"></param>
        /// <param name="questions"></param>
        /// <param name="answers"></param>
        /// <param name="authority"></param>
        /// <param name="additional"></param>
        public DnsResponseMessage(Header header, IList<Question> questions, IList<IResourceRecord> answers,
                IList<IResourceRecord> authority, IList<IResourceRecord> additional)
        {
            this.header = header;
            this.questions = questions;
            this.answers = answers;
            this.authority = authority;
            this.additional = additional;
        }

        /// <summary>
        /// Dns回复数据消息体
        /// </summary>
        public DnsResponseMessage()
        {
            this.header = new Header();
            this.questions = new List<Question>();
            this.answers = new List<IResourceRecord>();
            this.authority = new List<IResourceRecord>();
            this.additional = new List<IResourceRecord>();

            this.header.Response = true;
            this.header.Id = RANDOM.Next(UInt16.MaxValue);
        }

        /// <summary>
        /// Dns回复数据消息体
        /// </summary>
        /// <param name="response"></param>
        public DnsResponseMessage(IResponse response)
        {
            this.header = new Header();
            this.questions = new List<Question>(response.Questions);
            this.answers = new List<IResourceRecord>(response.AnswerRecords);
            this.authority = new List<IResourceRecord>(response.AuthorityRecords);
            this.additional = new List<IResourceRecord>(response.AdditionalRecords);

            this.header.Response = true;

            Id = response.Id;
            RecursionAvailable = response.RecursionAvailable;
            AuthorativeServer = response.AuthorativeServer;
            OperationCode = response.OperationCode;
            ResponseCode = response.ResponseCode;
        }

        /// <summary>
        /// 响应的问题列表
        /// </summary>
        public IList<Question> Questions
        {
            get { return questions; }
        }

        /// <summary>
        /// 应答资源记录列表
        /// </summary>
        public IList<IResourceRecord> AnswerRecords
        {
            get { return answers; }
        }

        /// <summary>
        /// 授权资源记录列表
        /// </summary>
        public IList<IResourceRecord> AuthorityRecords
        {
            get { return authority; }
        }

        /// <summary>
        /// 附加资源记录列表
        /// </summary>
        public IList<IResourceRecord> AdditionalRecords
        {
            get { return additional; }
        }

        /// <summary>
        /// 消息标识，与对应请求一致
        /// </summary>
        public int Id
        {
            get { return header.Id; }
            set { header.Id = value; }
        }

        /// <summary>
        /// 服务器是否支持递归查询
        /// </summary>
        public bool RecursionAvailable
        {
            get { return header.RecursionAvailable; }
            set { header.RecursionAvailable = value; }
        }

        /// <summary>
        /// 应答数据是否已通过 DNSSEC 验证
        /// </summary>
        public bool AuthenticData
        {
            get { return header.AuthenticData; }
            set { header.AuthenticData = value; }
        }

        /// <summary>
        /// 是否禁用 DNSSEC 校验
        /// </summary>
        public bool CheckingDisabled
        {
            get { return header.CheckingDisabled; }
            set { header.CheckingDisabled = value; }
        }

        /// <summary>
        /// 是否为授权服务器给出的应答
        /// </summary>
        public bool AuthorativeServer
        {
            get { return header.AuthorativeServer; }
            set { header.AuthorativeServer = value; }
        }

        /// <summary>
        /// 响应是否因超出传输长度而被截断
        /// </summary>
        public bool Truncated
        {
            get { return header.Truncated; }
            set { header.Truncated = value; }
        }

        /// <summary>
        /// 查询操作码
        /// </summary>
        public OperationCode OperationCode
        {
            get { return header.OperationCode; }
            set { header.OperationCode = value; }
        }

        /// <summary>
        /// 响应码
        /// </summary>
        public ResponseCode ResponseCode
        {
            get { return header.ResponseCode; }
            set { header.ResponseCode = value; }
        }

        /// <summary>
        /// 消息序列化后的总字节数
        /// </summary>
        public int Size
        {
            get
            {
                return header.Size +
                    questions.Sum(q => q.Size) +
                    answers.Sum(a => a.Size) +
                    authority.Sum(a => a.Size) +
                    additional.Sum(a => a.Size);
            }
        }

        /// <summary>
        /// 序列化为 DNS 响应报文，序列化前同步各段记录数
        /// </summary>
        /// <returns>响应报文字节</returns>
        public byte[] ToArray()
        {
            UpdateHeader();
            ByteStream result = new ByteStream(Size);

            result
                .Append(header.ToArray())
                .Append(questions.Select(q => q.ToArray()))
                .Append(answers.Select(a => a.ToArray()))
                .Append(authority.Select(a => a.ToArray()))
                .Append(additional.Select(a => a.ToArray()));

            return result.ToArray();
        }

        /// <summary>
        /// 输出消息头与各段内容的可读文本
        /// </summary>
        /// <returns>可读的消息描述</returns>
        public override string ToString()
        {
            UpdateHeader();

            return ObjectStringifier.New(this)
                .Add("Header", header)
                .Add("Questions", "AnswerRecords", "AuthorityRecords", "AdditionalRecords")
                .ToString();
        }

        private void UpdateHeader()
        {
            header.QuestionCount = questions.Count;
            header.AnswerRecordCount = answers.Count;
            header.AuthorityRecordCount = authority.Count;
            header.AdditionalRecordCount = additional.Count;
        }
    }
}
