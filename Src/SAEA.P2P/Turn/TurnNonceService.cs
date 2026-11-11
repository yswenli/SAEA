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
*命名空间：SAEA.P2P.Turn
*文件名： TurnNonceService
*版本号： v26.11.5.1
*唯一标识：9b574797-76a6-439b-8a9c-d456890dddc8
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 23:30:00
*描述：TURN nonce 签发与校验
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 23:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN nonce 签发与校验
*
*****************************************************************************/
using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace SAEA.P2P.Turn
{
    /// <summary>
    /// TURN nonce：值区为 8 字节时间戳加 16 字节截断 HMAC-SHA1，HMAC 输入含客户端源 IP 与端口，
    /// 因此同一个 nonce 不能换到另一个源地址上复用；默认有效期 600 秒。
    /// </summary>
    public sealed class TurnNonceService
    {
        /// <summary>
        /// nonce 内时间戳长度，单位字节
        /// </summary>
        private const int TimestampLength = 8;

        /// <summary>
        /// nonce 内 HMAC 截断长度，单位字节
        /// </summary>
        private const int MacLength = 16;

        /// <summary>
        /// nonce 总长度，单位字节
        /// </summary>
        private const int NonceLength = TimestampLength + MacLength;

        /// <summary>
        /// 服务端密钥；只用于 nonce 的 HMAC，不对外暴露
        /// </summary>
        private readonly byte[] _secret;

        /// <summary>
        /// nonce 有效期，单位秒
        /// </summary>
        private readonly int _lifetimeSeconds;

        /// <summary>
        /// 可注入时钟，用于把有效期压到用例可接受的时长
        /// </summary>
        private readonly Func<DateTimeOffset> _clock;

        /// <summary>
        /// 创建 nonce 服务
        /// </summary>
        /// <param name="secret">服务端密钥，必须非空</param>
        /// <param name="lifetimeSeconds">nonce 有效期，单位秒，必须大于 0</param>
        /// <param name="clock">取当前时间的委托；为 null 时使用系统 UTC 时间</param>
        /// <exception cref="ArgumentException">secret 为空或 lifetimeSeconds 不是正数</exception>
        public TurnNonceService(string secret, int lifetimeSeconds = 600, Func<DateTimeOffset> clock = null)
        {
            if (string.IsNullOrEmpty(secret))
            {
                throw new ArgumentException("nonce 密钥不能为空", nameof(secret));
            }
            if (lifetimeSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lifetimeSeconds), "nonce 有效期必须大于 0");
            }

            _secret = Encoding.UTF8.GetBytes(secret);
            _lifetimeSeconds = lifetimeSeconds;
            _clock = clock ?? (() => DateTimeOffset.UtcNow);
        }

        /// <summary>
        /// nonce 有效期，单位秒
        /// </summary>
        public int LifetimeSeconds
        {
            get { return _lifetimeSeconds; }
        }

        /// <summary>
        /// 为指定客户端源地址签发 nonce
        /// </summary>
        /// <param name="clientEndPoint">客户端源地址，含 IP 与端口</param>
        /// <returns>Base64 形式的 nonce</returns>
        /// <exception cref="ArgumentNullException">clientEndPoint 为 null</exception>
        public string Create(IPEndPoint clientEndPoint)
        {
            if (clientEndPoint == null)
            {
                throw new ArgumentNullException(nameof(clientEndPoint));
            }

            var timestamp = _clock().ToUnixTimeSeconds();
            var mac = ComputeMac(timestamp, clientEndPoint);
            var raw = new byte[NonceLength];
            WriteInt64(raw, 0, timestamp);
            Buffer.BlockCopy(mac, 0, raw, TimestampLength, MacLength);
            return Convert.ToBase64String(raw);
        }

        /// <summary>
        /// 校验 nonce 是否由本服务为同一源地址签发且尚未过期
        /// </summary>
        /// <param name="nonce">待校验的 nonce</param>
        /// <param name="clientEndPoint">客户端源地址</param>
        /// <param name="expired">为 true 表示签名正确但已过期，调用方应回 438；签名不符或来源不符时为 false</param>
        /// <returns>通过校验返回 true</returns>
        public bool TryValidate(string nonce, IPEndPoint clientEndPoint, out bool expired)
        {
            expired = false;
            if (string.IsNullOrEmpty(nonce) || clientEndPoint == null)
            {
                return false;
            }

            byte[] raw;
            try
            {
                raw = Convert.FromBase64String(nonce);
            }
            catch (FormatException)
            {
                return false;
            }

            if (raw.Length != NonceLength)
            {
                return false;
            }

            var timestamp = ReadInt64(raw, 0);
            var expected = ComputeMac(timestamp, clientEndPoint);
            if (!FixedTimeEquals(raw, TimestampLength, expected, 0, MacLength))
            {
                return false;
            }

            var now = _clock().ToUnixTimeSeconds();
            if (timestamp > now)
            {
                return false;
            }

            expired = now - timestamp >= _lifetimeSeconds;
            return !expired;
        }

        /// <summary>
        /// 计算 nonce 的截断 HMAC：输入为 8 字节时间戳与客户端源地址文本
        /// </summary>
        /// <param name="timestamp">签发时间戳，单位秒</param>
        /// <param name="clientEndPoint">客户端源地址</param>
        /// <returns>16 字节 HMAC</returns>
        private byte[] ComputeMac(long timestamp, IPEndPoint clientEndPoint)
        {
            var addressBytes = Encoding.UTF8.GetBytes(clientEndPoint.ToString());
            var input = new byte[TimestampLength + addressBytes.Length];
            WriteInt64(input, 0, timestamp);
            Buffer.BlockCopy(addressBytes, 0, input, TimestampLength, addressBytes.Length);

            using (var hmac = new HMACSHA1(_secret))
            {
                var full = hmac.ComputeHash(input);
                var result = new byte[MacLength];
                Buffer.BlockCopy(full, 0, result, 0, MacLength);
                return result;
            }
        }

        /// <summary>
        /// 定长比较两段字节，避免按字节提前返回泄露信息
        /// </summary>
        /// <param name="left">左缓冲</param>
        /// <param name="leftOffset">左缓冲起始位置</param>
        /// <param name="right">右缓冲</param>
        /// <param name="rightOffset">右缓冲起始位置</param>
        /// <param name="count">比较字节数</param>
        /// <returns>完全一致返回 true</returns>
        private static bool FixedTimeEquals(byte[] left, int leftOffset, byte[] right, int rightOffset, int count)
        {
            var diff = 0;
            for (var i = 0; i < count; i++)
            {
                diff |= left[leftOffset + i] ^ right[rightOffset + i];
            }
            return diff == 0;
        }

        /// <summary>
        /// 按大端写入 64 位整数
        /// </summary>
        /// <param name="buffer">目标缓冲</param>
        /// <param name="offset">写入位置</param>
        /// <param name="value">待写入值</param>
        private static void WriteInt64(byte[] buffer, int offset, long value)
        {
            for (var i = 0; i < 8; i++)
            {
                buffer[offset + i] = (byte)(value >> (56 - (i * 8)));
            }
        }

        /// <summary>
        /// 按大端读取 64 位整数
        /// </summary>
        /// <param name="buffer">来源缓冲</param>
        /// <param name="offset">读取位置</param>
        /// <returns>读到的值</returns>
        private static long ReadInt64(byte[] buffer, int offset)
        {
            long value = 0;
            for (var i = 0; i < 8; i++)
            {
                value = (value << 8) | buffer[offset + i];
            }
            return value;
        }
    }
}