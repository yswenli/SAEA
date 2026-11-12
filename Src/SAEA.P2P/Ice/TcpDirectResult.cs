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
*命名空间：SAEA.P2P.Ice
*文件名： TcpDirectResult
*版本号： v26.11.5.1
*唯一标识：11a225f5-07a4-4d83-bf0c-af3c91125688
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 13:33:14
*描述：TCP 直连检查结果
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 13:33:14
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TCP 直连检查结果
*
*****************************************************************************/
using System;
using System.Net;

namespace SAEA.P2P.Ice
{
    /// <summary>
    /// TCP 直连检查结果：成功与否、失败原因、错误码与耗时。
    /// 只由 <see cref="TcpDirectChannel"/> 填充，调用方只读。
    /// </summary>
    public sealed class TcpDirectResult
    {
        /// <summary>
        /// 由通道填充结果，不对外公开构造
        /// </summary>
        /// <param name="succeeded">检查是否通过</param>
        /// <param name="failure">失败原因，成功时为 None</param>
        /// <param name="errorCode">STUN 错误码，没有时为 0</param>
        /// <param name="errorReason">失败原因描述，成功时为 null</param>
        /// <param name="mappedAddress">检查阶段拿到的本地映射地址，可能为 null</param>
        /// <param name="remoteEndPoint">本次检查使用的远端端点</param>
        /// <param name="elapsed">从开始建链到检查结束的耗时</param>
        internal TcpDirectResult(bool succeeded, TcpDirectFailure failure, int errorCode, string errorReason, IPEndPoint mappedAddress, IPEndPoint remoteEndPoint, TimeSpan elapsed)
        {
            Succeeded = succeeded;
            Failure = failure;
            ErrorCode = errorCode;
            ErrorReason = errorReason;
            MappedAddress = mappedAddress;
            RemoteEndPoint = remoteEndPoint;
            Elapsed = elapsed;
        }

        /// <summary>
        /// 检查是否通过；为 true 时通道已处在数据面可用状态
        /// </summary>
        public bool Succeeded { get; }

        /// <summary>
        /// 失败原因；成功时为 <see cref="TcpDirectFailure.None"/>
        /// </summary>
        public TcpDirectFailure Failure { get; }

        /// <summary>
        /// STUN 错误码，仅 <see cref="TcpDirectFailure.Rejected"/> 时可能非 0；应答缺少 ERROR-CODE 时为 0
        /// </summary>
        public int ErrorCode { get; }

        /// <summary>
        /// 失败原因描述，成功时为 null
        /// </summary>
        public string ErrorReason { get; }

        /// <summary>
        /// 检查阶段拿到的本地映射地址，仅主动侧成功时有值；被动侧不回自己的映射地址，为 null
        /// </summary>
        public IPEndPoint MappedAddress { get; }

        /// <summary>
        /// 本次检查使用的远端端点。失败时用它把该远端候选对应的所有 pair 判为 Failed；
        /// 接受侧拿不到对端端点，因此可能为 null
        /// </summary>
        public IPEndPoint RemoteEndPoint { get; }

        /// <summary>
        /// 从开始建链到检查结束（含重试与退避）的耗时
        /// </summary>
        public TimeSpan Elapsed { get; }

        /// <summary>
        /// 构造成功结果
        /// </summary>
        /// <param name="mappedAddress">检查阶段拿到的本地映射地址，可为 null</param>
        /// <param name="remoteEndPoint">本次检查使用的远端端点，可为 null</param>
        /// <param name="elapsed">总耗时</param>
        /// <returns>成功结果</returns>
        internal static TcpDirectResult Success(IPEndPoint mappedAddress, IPEndPoint remoteEndPoint, TimeSpan elapsed)
        {
            return new TcpDirectResult(true, TcpDirectFailure.None, 0, null, mappedAddress, remoteEndPoint, elapsed);
        }

        /// <summary>
        /// 构造没有 STUN 错误码的失败结果
        /// </summary>
        /// <param name="failure">失败原因</param>
        /// <param name="reason">失败原因描述</param>
        /// <param name="remoteEndPoint">本次检查使用的远端端点，可为 null</param>
        /// <param name="elapsed">总耗时</param>
        /// <returns>失败结果</returns>
        internal static TcpDirectResult Fail(TcpDirectFailure failure, string reason, IPEndPoint remoteEndPoint, TimeSpan elapsed)
        {
            return new TcpDirectResult(false, failure, 0, reason, null, remoteEndPoint, elapsed);
        }

        /// <summary>
        /// 构造带 STUN 错误码的失败结果
        /// </summary>
        /// <param name="failure">失败原因</param>
        /// <param name="errorCode">STUN 错误码</param>
        /// <param name="reason">失败原因描述</param>
        /// <param name="remoteEndPoint">本次检查使用的远端端点，可为 null</param>
        /// <param name="elapsed">总耗时</param>
        /// <returns>失败结果</returns>
        internal static TcpDirectResult Fail(TcpDirectFailure failure, int errorCode, string reason, IPEndPoint remoteEndPoint, TimeSpan elapsed)
        {
            return new TcpDirectResult(false, failure, errorCode, reason, null, remoteEndPoint, elapsed);
        }
    }
}