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
*文件名： DnsRequestMessage
*版本号： v26.4.23.1
*唯一标识：2a25578a-420b-4eec-82f4-79d92c735f84
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 22:58:12
*描述：DnsRequestMessage消息类
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
    /// Dns请求消息体
    /// </summary>
    public class DnsRequestMessage : IRequest
    {
        private static readonly Random RANDOM = new Random();

        private IList<Question> questions;
        private Header header;
        private IList<IResourceRecord> additional;

        /// <summary>
        /// 从字节数组解析请求消息
        /// </summary>
        /// <param name="message">请求消息字节</param>
        /// <returns>解析后的请求消息</returns>
        public static DnsRequestMessage FromArray(byte[] message)
        {
            Header header = Header.FromArray(message);
            int offset = header.Size;

            if (header.Response || header.QuestionCount == 0 ||
                    header.AnswerRecordCount + header.AuthorityRecordCount > 0 ||
                    header.ResponseCode != ResponseCode.NoError)
            {

                throw new ArgumentException("Invalid request message");
            }

            return new DnsRequestMessage(header,
                Question.GetAllFromArray(message, offset, header.QuestionCount, out offset),
                ResourceRecordFactory.GetAllFromArray(message, offset, header.AdditionalRecordCount, out offset));
        }

        /// <summary>
        /// Dns请求消息体
        /// </summary>
        /// <param name="header"></param>
        /// <param name="questions"></param>
        /// <param name="additional"></param>
        public DnsRequestMessage(Header header, IList<Question> questions, IList<IResourceRecord> additional)
        {
            this.header = header;
            this.questions = questions;
            this.additional = additional;
        }

        /// <summary>
        /// Dns请求消息体
        /// </summary>
        public DnsRequestMessage()
        {
            this.questions = new List<Question>();
            this.header = new Header();
            this.additional = new List<IResourceRecord>();

            this.header.OperationCode = OperationCode.Query;
            this.header.Response = false;
            this.header.Id = RANDOM.Next(UInt16.MaxValue);
        }

        /// <summary>
        /// Dns请求消息体
        /// </summary>
        /// <param name="request"></param>
        public DnsRequestMessage(IRequest request)
        {
            this.header = new Header();
            this.questions = new List<Question>(request.Questions);
            this.additional = new List<IResourceRecord>(request.AdditionalRecords);

            this.header.Response = false;

            Id = request.Id;
            OperationCode = request.OperationCode;
            RecursionDesired = request.RecursionDesired;
        }

        /// <summary>
        /// 请求的问题列表
        /// </summary>
        public IList<Question> Questions
        {
            get { return questions; }
        }

        /// <summary>
        /// 附加资源记录列表
        /// </summary>
        public IList<IResourceRecord> AdditionalRecords
        {
            get { return additional; }
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
                    additional.Sum(a => a.Size);
            }
        }

        /// <summary>
        /// 消息标识，占 2 字节
        /// </summary>
        public int Id
        {
            get { return header.Id; }
            set { header.Id = value; }
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
        /// 是否期望递归查询
        /// </summary>
        public bool RecursionDesired
        {
            get { return header.RecursionDesired; }
            set { header.RecursionDesired = value; }
        }

        /// <summary>
        /// 序列化为 DNS 请求报文，序列化前同步问题数与附加记录数
        /// </summary>
        /// <returns>请求报文字节</returns>
        public byte[] ToArray()
        {
            UpdateHeader();
            ByteStream result = new ByteStream(Size);

            result
                .Append(header.ToArray())
                .Append(questions.Select(q => q.ToArray()))
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
                .Add("Questions", "AdditionalRecords")
                .ToString();
        }

        private void UpdateHeader()
        {
            header.QuestionCount = questions.Count;
            header.AdditionalRecordCount = additional.Count;
        }
    }
}
