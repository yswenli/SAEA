/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.P2P.Stun
*文件名： StunBindingResult
*版本号： v26.11.5.1
*唯一标识：341f5638-16fc-42d8-91af-f63f354d31f6
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 17:20:00
*描述：Binding 请求结果模型
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 17:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 Binding 请求结果模型
*
*****************************************************************************/
using System;
using System.Net;

namespace SAEA.P2P.Stun
{
    /// <summary>
    /// Binding 请求结果：成功时带映射地址与响应来源，失败时带原因与 STUN 错误码。
    /// 所有地址字段都是数值，调用方不再持有任何底层缓冲。
    /// </summary>
    public sealed class StunBindingResult
    {
        /// <summary>
        /// 创建结果；只由 <see cref="Success"/> 与 <see cref="Failed"/> 调用
        /// </summary>
        private StunBindingResult()
        {
        }

        /// <summary>
        /// 是否拿到成功应答
        /// </summary>
        public bool Succeeded { get; private set; }

        /// <summary>
        /// 失败原因；成功时为 <see cref="StunBindingFailure.None"/>
        /// </summary>
        public StunBindingFailure Failure { get; private set; }

        /// <summary>
        /// 服务端返回的 STUN 错误码；没有错误应答时为 0
        /// </summary>
        public int ErrorCode { get; private set; }

        /// <summary>
        /// 错误码对应的原因短语；没有错误应答时为 null
        /// </summary>
        public string ErrorReason { get; private set; }

        /// <summary>
        /// 映射地址；优先取 XOR-MAPPED-ADDRESS，缺失时取 MAPPED-ADDRESS
        /// </summary>
        public IPEndPoint MappedAddress { get; private set; }

        /// <summary>
        /// 异或映射地址；应答未携带时为 null
        /// </summary>
        public IPEndPoint XorMappedAddress { get; private set; }

        /// <summary>
        /// 响应来源地址；用于 RFC 5780 判断是哪个绑定地址回的包
        /// </summary>
        public IPEndPoint ResponseOrigin { get; private set; }

        /// <summary>
        /// 备用地址；服务端未提供时为 null
        /// </summary>
        public IPEndPoint OtherAddress { get; private set; }

        /// <summary>
        /// 认证域；仅错误应答携带时为非空
        /// </summary>
        public string Realm { get; private set; }

        /// <summary>
        /// 一次性随机串；仅错误应答携带时为非空，调用方重试时必须复用它
        /// </summary>
        public string Nonce { get; private set; }

        /// <summary>
        /// 从开始到拿到最终应答的耗时；不含调用方排队时间
        /// </summary>
        public TimeSpan Elapsed { get; private set; }

        /// <summary>
        /// 本轮实际发送次数，含重传与凭据重试
        /// </summary>
        public int Attempts { get; private set; }

        /// <summary>
        /// 构造成功结果
        /// </summary>
        /// <param name="xorMappedAddress">异或映射地址</param>
        /// <param name="mappedAddress">映射地址</param>
        /// <param name="responseOrigin">响应来源</param>
        /// <param name="otherAddress">备用地址</param>
        /// <param name="elapsed">耗时</param>
        /// <param name="attempts">发送次数</param>
        /// <returns>成功结果</returns>
        internal static StunBindingResult Success(IPEndPoint xorMappedAddress, IPEndPoint mappedAddress, IPEndPoint responseOrigin, IPEndPoint otherAddress, TimeSpan elapsed, int attempts)
        {
            return new StunBindingResult
            {
                Succeeded = true,
                Failure = StunBindingFailure.None,
                MappedAddress = xorMappedAddress ?? mappedAddress,
                XorMappedAddress = xorMappedAddress,
                ResponseOrigin = responseOrigin,
                OtherAddress = otherAddress,
                Elapsed = elapsed,
                Attempts = attempts
            };
        }

        /// <summary>
        /// 构造失败结果
        /// </summary>
        /// <param name="failure">失败原因</param>
        /// <param name="errorCode">STUN 错误码，没有错误应答时传 0</param>
        /// <param name="errorReason">错误原因短语</param>
        /// <param name="elapsed">耗时</param>
        /// <param name="attempts">发送次数</param>
        /// <returns>失败结果</returns>
        internal static StunBindingResult Failed(StunBindingFailure failure, int errorCode, string errorReason, TimeSpan elapsed, int attempts)
        {
            return new StunBindingResult
            {
                Succeeded = false,
                Failure = failure,
                ErrorCode = errorCode,
                ErrorReason = errorReason,
                Elapsed = elapsed,
                Attempts = attempts
            };
        }

        /// <summary>
        /// 给失败结果补上错误应答里的认证参数；只有 401/438 会用到
        /// </summary>
        /// <param name="realm">认证域</param>
        /// <param name="nonce">一次性随机串</param>
        internal void SetChallenge(string realm, string nonce)
        {
            Realm = realm;
            Nonce = nonce;
        }
    }
}