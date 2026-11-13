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
*命名空间：SAEA.P2P.Discovery
*文件名： DiscoveryJsonCodec
*版本号： v26.11.5.1
*唯一标识：93341705-f3bf-42a0-8b3e-2166514e9524
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 16:59:24
*描述：无信令局域网发现报文的 JSON 编解码与 HMAC 签名
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 16:59:24
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增无信令局域网发现报文的 JSON 编解码与 HMAC 签名
*
*****************************************************************************/
using System;
using System.Text;
using SAEA.Common.Newtonsoft.Json;
using SAEA.Common.Newtonsoft.Json.Serialization;
using SAEA.P2P.Transport;

namespace SAEA.P2P.Discovery
{
    /// <summary>
    /// 无信令局域网发现报文的 JSON 编解码：字段名 camelCase，未知字段忽略；
    /// HMAC 覆盖 Hmac 置空后的规范负载，长度上限与 UDP 单包上限一致。
    /// </summary>
    public static class DiscoveryJsonCodec
    {
        /// <summary>
        /// 当前发现协议版本
        /// </summary>
        public const int CurrentProtocolVersion = 1;

        /// <summary>
        /// 序列化设置：字段名统一 camelCase，未知字段忽略、缺失字段按默认值、空值不写出
        /// </summary>
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };

        /// <summary>
        /// 把发现报文原样序列化为 UTF-8 JSON 负载，Hmac 按报文当前值写出
        /// </summary>
        /// <param name="packet">发现报文</param>
        /// <returns>负载字节</returns>
        /// <exception cref="ArgumentNullException">报文为空</exception>
        public static byte[] Encode(DiscoveryPacket packet)
        {
            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }
            return Serialize(packet);
        }

        /// <summary>
        /// 按共享密钥签名并序列化发现报文：先对 Hmac 置空后的规范负载计算标签，再把标签写回报文后输出
        /// </summary>
        /// <param name="packet">发现报文，签名后 Hmac 为计算出的标签</param>
        /// <param name="key">共享密钥，按 UTF-8 参与计算；空表示不启用认证</param>
        /// <returns>含标签的 UTF-8 JSON 负载</returns>
        /// <exception cref="ArgumentNullException">报文为空</exception>
        public static byte[] Encode(DiscoveryPacket packet, string key)
        {
            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            packet.Hmac = null;
            packet.Hmac = DiscoveryHmac.Compute(key, Serialize(packet));
            return Serialize(packet);
        }

        /// <summary>
        /// 生成 HMAC 覆盖的规范负载：Hmac 置空后按同一 JSON 设置序列化
        /// </summary>
        /// <param name="packet">发现报文，调用前后 Hmac 值保持不变</param>
        /// <returns>规范负载字节</returns>
        /// <exception cref="ArgumentNullException">报文为空</exception>
        public static byte[] GetHmacPayload(DiscoveryPacket packet)
        {
            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            var original = packet.Hmac;
            try
            {
                packet.Hmac = null;
                return Serialize(packet);
            }
            finally
            {
                packet.Hmac = original;
            }
        }

        /// <summary>
        /// 解析发现报文，只做长度、版本与结构校验，不校验 HMAC；任何失败都返回 false 而不抛异常
        /// </summary>
        /// <param name="payload">负载字节，可为 null</param>
        /// <param name="packet">解析成功的报文，失败时为 null</param>
        /// <param name="error">失败原因，成功时为 null</param>
        /// <returns>结构合法且版本匹配返回 true</returns>
        public static bool TryDecode(byte[] payload, out DiscoveryPacket packet, out string error)
        {
            packet = null;

            if (payload == null || payload.Length == 0)
            {
                error = "发现负载为空";
                return false;
            }

            // 超过 UDP 单包上限的负载不可能来自合法发送方，先按长度丢弃再做任何解析
            if (payload.Length > DataPlaneCodec.MaxUdpDatagramLength)
            {
                error = "发现负载超过单包上限：" + payload.Length + " 字节";
                return false;
            }

            DiscoveryPacket parsed;
            try
            {
                var text = Encoding.UTF8.GetString(payload);
                parsed = JsonConvert.DeserializeObject<DiscoveryPacket>(text, Settings);
            }
            catch (Exception ex)
            {
                // 外部输入不可信，任何解析异常都转为失败结果交给上层处理
                error = "发现负载无法解析：" + ex.Message;
                return false;
            }

            if (parsed == null)
            {
                error = "发现负载解析结果为空";
                return false;
            }

            if (parsed.Version != CurrentProtocolVersion)
            {
                error = "发现协议版本不匹配：期望 " + CurrentProtocolVersion + "，实际 " + parsed.Version;
                return false;
            }

            packet = parsed;
            error = null;
            return true;
        }

        /// <summary>
        /// 解析发现报文并校验 HMAC，任何失败都返回 false 而不抛异常
        /// </summary>
        /// <param name="payload">收到的完整负载字节</param>
        /// <param name="key">共享密钥；空表示不启用认证，此时只接受未签名的报文</param>
        /// <param name="packet">解析并验签通过的报文，失败时为 null</param>
        /// <param name="error">失败原因，成功时为 null</param>
        /// <returns>结构合法、版本匹配且标签一致返回 true</returns>
        public static bool TryDecode(byte[] payload, string key, out DiscoveryPacket packet, out string error)
        {
            if (!TryDecode(payload, out packet, out error))
            {
                return false;
            }

            // 校验用重算的规范负载，而不是收到的原始字节，避免字段顺序或空白差异影响结果
            if (!DiscoveryHmac.Verify(key, GetHmacPayload(packet), packet.Hmac))
            {
                packet = null;
                error = "发现报文 HMAC 校验失败";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// 按统一设置序列化为 UTF-8 字节
        /// </summary>
        /// <param name="packet">发现报文</param>
        /// <returns>负载字节</returns>
        private static byte[] Serialize(DiscoveryPacket packet)
        {
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(packet, Settings));
        }
    }
}