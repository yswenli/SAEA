/****************************************************************************
*项目名称：SAEA.DNS
*CLR 版本：3.0
*机器名称：WENLI-PC
*命名空间：SAEA.DNS.Model
*类 名 称：DnsClientResponse
*版 本 号：v5.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/11/28 22:43:28
*描述：
*=====================================================================
*修改时间：2019/11/28 22:43:28
*修 改 人： yswenli
*版本号： v7.0.0.1
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
    public class DnsClientResponse : IResponse
    {
        private IResponse _response;

        private byte[] _message;

        public static DnsClientResponse FromArray(IRequest request, byte[] message)
        {
            DnsResponseMessage response = DnsResponseMessage.FromArray(message);

            return new DnsClientResponse(request, response, message);
        }

        /// <summary>
        /// 响应
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="message"></param>
        internal DnsClientResponse(IRequest request, IResponse response, byte[] message)
        {
            Request = request;
            this._message = message;
            this._response = response;
        }
        /// <summary>
        /// 响应
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        internal DnsClientResponse(IRequest request, IResponse response)
        {
            Request = request;

            this._message = response.ToArray();
            this._response = response;
        }

        public IRequest Request
        {
            get;
            private set;
        }

        public int Id
        {
            get { return _response.Id; }
            set { }
        }

        public IList<IResourceRecord> AnswerRecords
        {
            get { return _response.AnswerRecords; }
        }

        public IList<IResourceRecord> AuthorityRecords
        {
            get { return new ReadOnlyCollection<IResourceRecord>(_response.AuthorityRecords); }
        }

        public IList<IResourceRecord> AdditionalRecords
        {
            get { return new ReadOnlyCollection<IResourceRecord>(_response.AdditionalRecords); }
        }

        public bool RecursionAvailable
        {
            get { return _response.RecursionAvailable; }
            set { }
        }

        public bool AuthenticData
        {
            get { return _response.AuthenticData; }
            set { }
        }

        public bool CheckingDisabled
        {
            get { return _response.CheckingDisabled; }
            set { }
        }

        public bool AuthorativeServer
        {
            get { return _response.AuthorativeServer; }
            set { }
        }

        public bool Truncated
        {
            get { return _response.Truncated; }
            set { }
        }

        public OperationCode OperationCode
        {
            get { return _response.OperationCode; }
            set { }
        }

        public ResponseCode ResponseCode
        {
            get { return _response.ResponseCode; }
            set { }
        }

        public IList<Question> Questions
        {
            get { return new ReadOnlyCollection<Question>(_response.Questions); }
        }

        public int Size
        {
            get { return _message.Length; }
        }

        public byte[] ToArray()
        {
            return _message;
        }

        public override string ToString()
        {
            return _response.ToString();
        }
    }
}
