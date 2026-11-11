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
*文件名： StunTransactionResult
*版本号： v26.11.5.1
*唯一标识：429d069e-fc7e-4fe9-a6f3-8cfa7b317e9d
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 16:20:00
*描述：STUN 事务结果
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 16:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 STUN 事务结果
*
*****************************************************************************/
namespace SAEA.P2P.Stun
{
    /// <summary>
    /// STUN 事务结果：成功时带对端应答，失败时带原因与实际发送次数
    /// </summary>
    public sealed class StunTransactionResult
    {
        /// <summary>
        /// 创建事务结果
        /// </summary>
        /// <param name="response">成功时的应答消息，失败时为 null</param>
        /// <param name="failure">失败原因，成功时为 None</param>
        /// <param name="attempts">实际发送次数</param>
        private StunTransactionResult(StunMessage response, StunTransactionFailure failure, int attempts)
        {
            Response = response;
            Failure = failure;
            Attempts = attempts;
        }

        /// <summary>
        /// 是否收到应答；错误应答也属于收到应答，需检查 <see cref="Response"/> 的类别
        /// </summary>
        public bool Succeeded
        {
            get { return Response != null; }
        }

        /// <summary>
        /// 对端应答；超时、取消或发送失败时为 null
        /// </summary>
        public StunMessage Response { get; }

        /// <summary>
        /// 失败原因；成功时为 None
        /// </summary>
        public StunTransactionFailure Failure { get; }

        /// <summary>
        /// 实际发送次数，含首发
        /// </summary>
        public int Attempts { get; }

        /// <summary>
        /// 构造成功结果
        /// </summary>
        /// <param name="response">对端应答</param>
        /// <param name="attempts">实际发送次数</param>
        /// <returns>事务结果</returns>
        public static StunTransactionResult Success(StunMessage response, int attempts)
        {
            return new StunTransactionResult(response, StunTransactionFailure.None, attempts);
        }

        /// <summary>
        /// 构造失败结果
        /// </summary>
        /// <param name="failure">失败原因</param>
        /// <param name="attempts">实际发送次数</param>
        /// <returns>事务结果</returns>
        public static StunTransactionResult Failed(StunTransactionFailure failure, int attempts)
        {
            return new StunTransactionResult(null, failure, attempts);
        }
    }
}