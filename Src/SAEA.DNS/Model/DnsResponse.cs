/****************************************************************************
*项目名称：SAEA.DNS
*CLR 版本：3.0
*机器名称：WENLI-PC
*命名空间：SAEA.DNS.Model
*类 名 称：DnsResponse
*版 本 号：v5.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/11/28 22:43:28
*描述：
*=====================================================================
*修改时间：2019/11/28 22:43:28
*修 改 人： yswenli
*版 本 号： v5.0.0.1
*描    述：
*****************************************************************************/
using SAEA.DNS.Common.ResourceRecords;
using SAEA.DNS.Protocol;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SAEA.DNS.Model
{
    /// <summary>
    /// 响应
    /// </summary>
    public class DnsResponse : IResponse
    {
        private IResponse response;
        private byte[] message;

        public static DnsResponse FromArray(IRequest request, byte[] message)
        {
            Protocol.DnsResponseMessage response = Protocol.DnsResponseMessage.FromArray(message);
            return new DnsResponse(request, response, message);
        }

        internal DnsResponse(IRequest request, IResponse response, byte[] message)
        {
            Request = request;
            this.message = message;
            this.response = response;
        }

        internal DnsResponse(IRequest request, IResponse response)
        {
            Request = request;

            this.message = response.ToArray();
            this.response = response;
        }

        public IRequest Request
        {
            get;
            private set;
        }

        public int Id
        {
            get { return response.Id; }
            set { }
        }

        public IList<IResourceRecord> AnswerRecords
        {
            get { return response.AnswerRecords; }
        }

        public IList<IResourceRecord> AuthorityRecords
        {
            get { return new ReadOnlyCollection<IResourceRecord>(response.AuthorityRecords); }
        }

        public IList<IResourceRecord> AdditionalRecords
        {
            get { return new ReadOnlyCollection<IResourceRecord>(response.AdditionalRecords); }
        }

        public bool RecursionAvailable
        {
            get { return response.RecursionAvailable; }
            set { }
        }

        public bool AuthenticData
        {
            get { return response.AuthenticData; }
            set { }
        }

        public bool CheckingDisabled
        {
            get { return response.CheckingDisabled; }
            set { }
        }

        public bool AuthorativeServer
        {
            get { return response.AuthorativeServer; }
            set { }
        }

        public bool Truncated
        {
            get { return response.Truncated; }
            set { }
        }

        public OperationCode OperationCode
        {
            get { return response.OperationCode; }
            set { }
        }

        public ResponseCode ResponseCode
        {
            get { return response.ResponseCode; }
            set { }
        }

        public IList<Question> Questions
        {
            get { return new ReadOnlyCollection<Question>(response.Questions); }
        }

        public int Size
        {
            get { return message.Length; }
        }

        public byte[] ToArray()
        {
            return message;
        }

        public override string ToString()
        {
            return response.ToString();
        }
    }
}
