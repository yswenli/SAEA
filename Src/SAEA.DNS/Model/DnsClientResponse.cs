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
*文件名： DnsClientResponse
*版本号： v26.4.23.1
*唯一标识：573e0147-ae46-4d70-ad65-90be609bab05
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/30 01:33:08
*描述：DnsClientResponse接口
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

        /// <summary>
        /// 从报文字节构造响应，并关联原请求
        /// </summary>
        /// <param name="request">对应的请求</param>
        /// <param name="message">原始响应报文</param>
        /// <returns>包装后的响应</returns>
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

        /// <summary>
        /// 该响应所对应的请求
        /// </summary>
        public IRequest Request
        {
            get;
            private set;
        }

        /// <summary>
        /// 报文标识，赋值被忽略
        /// </summary>
        public int Id
        {
            get { return _response.Id; }
            set { }
        }

        /// <summary>
        /// 回答段记录，直接返回底层可变集合
        /// </summary>
        public IList<IResourceRecord> AnswerRecords
        {
            get { return _response.AnswerRecords; }
        }

        /// <summary>
        /// 授权段记录，只读视图
        /// </summary>
        public IList<IResourceRecord> AuthorityRecords
        {
            get { return new ReadOnlyCollection<IResourceRecord>(_response.AuthorityRecords); }
        }

        /// <summary>
        /// 附加段记录，只读视图
        /// </summary>
        public IList<IResourceRecord> AdditionalRecords
        {
            get { return new ReadOnlyCollection<IResourceRecord>(_response.AdditionalRecords); }
        }

        /// <summary>
        /// 服务器是否支持递归查询，赋值被忽略
        /// </summary>
        public bool RecursionAvailable
        {
            get { return _response.RecursionAvailable; }
            set { }
        }

        /// <summary>
        /// 响应数据是否经过 DNSSEC 验证，赋值被忽略
        /// </summary>
        public bool AuthenticData
        {
            get { return _response.AuthenticData; }
            set { }
        }

        /// <summary>
        /// 请求方是否禁用了 DNSSEC 检查，赋值被忽略
        /// </summary>
        public bool CheckingDisabled
        {
            get { return _response.CheckingDisabled; }
            set { }
        }

        /// <summary>
        /// 服务器是否为该域的授权服务器，赋值被忽略
        /// </summary>
        public bool AuthorativeServer
        {
            get { return _response.AuthorativeServer; }
            set { }
        }

        /// <summary>
        /// 响应是否被截断，赋值被忽略
        /// </summary>
        public bool Truncated
        {
            get { return _response.Truncated; }
            set { }
        }

        /// <summary>
        /// 操作码，赋值被忽略
        /// </summary>
        public OperationCode OperationCode
        {
            get { return _response.OperationCode; }
            set { }
        }

        /// <summary>
        /// 响应码，赋值被忽略
        /// </summary>
        public ResponseCode ResponseCode
        {
            get { return _response.ResponseCode; }
            set { }
        }

        /// <summary>
        /// 问题段，只读视图
        /// </summary>
        public IList<Question> Questions
        {
            get { return new ReadOnlyCollection<Question>(_response.Questions); }
        }

        /// <summary>
        /// 原始报文的字节长度
        /// </summary>
        public int Size
        {
            get { return _message.Length; }
        }

        /// <summary>
        /// 返回原始报文数组本身，不做复制
        /// </summary>
        /// <returns>构造响应时传入的报文字节</returns>
        public byte[] ToArray()
        {
            return _message;
        }

        /// <summary>
        /// 输出底层响应的字段文本
        /// </summary>
        /// <returns>底层响应的 ToString 结果</returns>
        public override string ToString()
        {
            return _response.ToString();
        }
    }
}
