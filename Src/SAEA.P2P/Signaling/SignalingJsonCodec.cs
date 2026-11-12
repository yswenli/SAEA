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
*命名空间：SAEA.P2P.Signaling
*文件名： SignalingJsonCodec
*版本号： v26.11.5.1
*唯一标识：7fb9ca85-bf4e-48d5-ab54-2285aaa71dc8
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 18:15:22
*描述：信令报文 JSON 编解码
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 18:15:22
*修改人： yswenli
*版本号： v26.11.5.1
*描述：信令报文 JSON 编解码
*
*****************************************************************************/
using System;
using System.Text;
using SAEA.Common.Newtonsoft.Json;
using SAEA.Common.Newtonsoft.Json.Serialization;
using SAEA.P2P.Common;
using SAEA.P2P.Protocol;

namespace SAEA.P2P.Signaling
{
    /// <summary>
    /// 信令报文 JSON 编解码
    /// </summary>
    public static class SignalingJsonCodec
    {
        /// <summary>
        /// 当前信令协议版本
        /// </summary>
        public const int CurrentProtocolVersion = 1;

        /// <summary>
        /// 序列化设置：字段名统一 camelCase，未知字段忽略、缺失字段按默认值
        /// </summary>
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };

        /// <summary>
        /// 把信令报文序列化为 UTF-8 JSON 负载，不含帧头
        /// </summary>
        /// <param name="message">报文</param>
        /// <returns>负载字节</returns>
        public static byte[] Encode(SignalingMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message, Settings));
        }

        /// <summary>
        /// 把 UTF-8 JSON 负载解析为信令报文，解析失败或版本不匹配时返回 Error 报文而不抛异常
        /// </summary>
        /// <param name="payload">负载字节，可为 null</param>
        /// <returns>解析出的报文；失败时为 Type 为 Error 的报文</returns>
        public static SignalingMessage Decode(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return CreateError(ErrorCode.ProtocolMalformed, "信令负载为空");
            }

            SignalingMessage message;
            try
            {
                var text = Encoding.UTF8.GetString(payload);
                message = JsonConvert.DeserializeObject<SignalingMessage>(text, Settings);
            }
            catch (Exception ex)
            {
                // 外部输入不可信，任何解析异常都转为 Error 报文交给上层处理
                return CreateError(ErrorCode.ProtocolMalformed, "信令负载无法解析：" + ex.Message);
            }

            if (message == null)
            {
                return CreateError(ErrorCode.ProtocolMalformed, "信令负载解析结果为空");
            }

            if (message.Version != CurrentProtocolVersion)
            {
                return CreateError(ErrorCode.ProtocolVersionMismatch,
                    "信令协议版本不匹配：期望 " + CurrentProtocolVersion + "，实际 " + message.Version);
            }

            return message;
        }

        /// <summary>
        /// 构造 Error 报文
        /// </summary>
        /// <param name="code">错误码</param>
        /// <param name="text">错误说明</param>
        /// <returns>Error 报文</returns>
        public static SignalingMessage CreateError(string code, string text)
        {
            return new SignalingMessage
            {
                Type = P2PMessageType.Error,
                Code = code,
                Message = text
            };
        }
    }
}