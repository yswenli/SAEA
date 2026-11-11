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
*文件名： StunErrorCode
*版本号： v26.11.5.1
*唯一标识：a3dd8118-058b-4465-b45c-08e65184a260
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 17:22:00
*描述：STUN ERROR-CODE 属性编解码
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 17:22:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 STUN ERROR-CODE 属性编解码
*
*****************************************************************************/
using System;
using System.Text;

namespace SAEA.P2P.Stun
{
    /// <summary>
    /// STUN ERROR-CODE 属性：2 字节保留、1 字节错误类别、1 字节错误号，其后是 UTF-8 原因短语；
    /// 数值必须落在 300 到 699 之间
    /// </summary>
    public static class StunErrorCode
    {
        /// <summary>
        /// ERROR-CODE 属性值最小的固定部分长度
        /// </summary>
        public const int ValueHeaderLength = 4;

        /// <summary>
        /// 创建错误码属性
        /// </summary>
        /// <param name="code">错误码，取值 300 到 699</param>
        /// <param name="reason">原因短语，可为 null</param>
        /// <returns>错误码属性</returns>
        public static StunAttribute Create(int code, string reason)
        {
            if (code < 300 || code > 699)
            {
                throw new ArgumentOutOfRangeException(nameof(code));
            }

            var reasonBytes = string.IsNullOrEmpty(reason) ? new byte[0] : Encoding.UTF8.GetBytes(reason);
            var value = new byte[ValueHeaderLength + reasonBytes.Length];
            value[2] = (byte)(code / 100);
            value[3] = (byte)(code % 100);
            if (reasonBytes.Length > 0)
            {
                Buffer.BlockCopy(reasonBytes, 0, value, ValueHeaderLength, reasonBytes.Length);
            }

            return new StunAttribute(StunAttributeType.ErrorCode, value);
        }

        /// <summary>
        /// 从消息里读取第一个错误码属性
        /// </summary>
        /// <param name="message">应答消息</param>
        /// <param name="code">错误码</param>
        /// <param name="reason">原因短语，可能为空串</param>
        /// <returns>存在且格式正确时返回 true</returns>
        public static bool TryGet(StunMessage message, out int code, out string reason)
        {
            code = 0;
            reason = null;
            if (message == null)
            {
                return false;
            }

            StunAttribute attribute;
            if (!message.TryGet(StunAttributeType.ErrorCode, out attribute))
            {
                return false;
            }

            var value = attribute.Value.Span;
            if (value.Length < ValueHeaderLength)
            {
                return false;
            }

            code = ((value[2] & 0x07) * 100) + value[3];
            reason = value.Length > ValueHeaderLength
                ? Encoding.UTF8.GetString(attribute.Value.Span.Slice(ValueHeaderLength).ToArray())
                : string.Empty;
            return true;
        }
    }
}