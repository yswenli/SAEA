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
*文件名： TurnCredentialService
*版本号： v26.11.5.1
*唯一标识：775834c6-2b16-4962-8f0e-3bf32ed48314
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 23:30:00
*描述：TURN 临时凭据签发与第一层校验
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 23:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN 临时凭据签发与第一层校验
*
*****************************************************************************/
using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SAEA.P2P.Turn
{
    /// <summary>
    /// TURN 临时凭据服务：按 coturn REST 风格用共享密钥派生密码。
    /// 用户名是「过期 Unix 秒数 + 分隔符 + 用户标识」，密码是 Base64(HMAC-SHA1(共享密钥, 用户名))；
    /// 共享密钥只从构造参数读取，<see cref="ToString"/> 不输出密钥与密码。
    /// </summary>
    public sealed class TurnCredentialService
    {
        /// <summary>
        /// 默认的用户名分隔符
        /// </summary>
        public const string DefaultSeparator = ":";

        /// <summary>
        /// 默认的有效期，单位秒
        /// </summary>
        public const int DefaultTtlSeconds = 600;

        /// <summary>
        /// 共享密钥的 UTF-8 字节，作为 HMAC 密钥
        /// </summary>
        private readonly byte[] _secret;

        /// <summary>
        /// 认证域
        /// </summary>
        private readonly string _realm;

        /// <summary>
        /// 用户名分隔符
        /// </summary>
        private readonly string _separator;

        /// <summary>
        /// 凭据有效期，单位秒
        /// </summary>
        private readonly int _ttlSeconds;

        /// <summary>
        /// 可注入时钟，用于把过期判定压到用例可接受的时长
        /// </summary>
        private readonly Func<DateTimeOffset> _clock;

        /// <summary>
        /// 创建凭据服务
        /// </summary>
        /// <param name="sharedSecret">与信令服务器共享的密钥，不能为空</param>
        /// <param name="realm">认证域，不能为空</param>
        /// <param name="ttlSeconds">凭据有效期，单位秒，必须大于 0</param>
        /// <param name="separator">用户名分隔符，为空时用「:」</param>
        /// <param name="clock">取当前时间的委托；为 null 时使用系统 UTC 时间</param>
        /// <exception cref="ArgumentException">sharedSecret 或 realm 为空</exception>
        /// <exception cref="ArgumentOutOfRangeException">ttlSeconds 不是正数</exception>
        public TurnCredentialService(string sharedSecret, string realm, int ttlSeconds = DefaultTtlSeconds, string separator = DefaultSeparator, Func<DateTimeOffset> clock = null)
        {
            if (string.IsNullOrEmpty(sharedSecret))
            {
                throw new ArgumentException("共享密钥不能为空", nameof(sharedSecret));
            }
            if (string.IsNullOrEmpty(realm))
            {
                throw new ArgumentException("认证域不能为空", nameof(realm));
            }
            if (ttlSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ttlSeconds), "凭据有效期必须大于 0");
            }

            _secret = Encoding.UTF8.GetBytes(sharedSecret);
            _realm = realm;
            _separator = string.IsNullOrEmpty(separator) ? DefaultSeparator : separator;
            _ttlSeconds = ttlSeconds;
            _clock = clock ?? (() => DateTimeOffset.UtcNow);
        }

        /// <summary>
        /// 认证域
        /// </summary>
        public string Realm
        {
            get { return _realm; }
        }

        /// <summary>
        /// 用户名分隔符
        /// </summary>
        public string Separator
        {
            get { return _separator; }
        }

        /// <summary>
        /// 凭据有效期，单位秒
        /// </summary>
        public int TtlSeconds
        {
            get { return _ttlSeconds; }
        }

        /// <summary>
        /// 为指定用户签发临时凭据
        /// </summary>
        /// <param name="userId">用户标识，不能为空且不能含分隔符</param>
        /// <returns>临时凭据</returns>
        /// <exception cref="ArgumentException">userId 为空或含分隔符</exception>
        public TurnCredential Create(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("用户标识不能为空", nameof(userId));
            }
            if (userId.IndexOf(_separator, StringComparison.Ordinal) >= 0)
            {
                throw new ArgumentException("用户标识不能包含用户名分隔符", nameof(userId));
            }

            var expiresAt = _clock().AddSeconds(_ttlSeconds);
            var expiresAtUnixSeconds = expiresAt.ToUnixTimeSeconds();
            var username = expiresAtUnixSeconds.ToString(CultureInfo.InvariantCulture) + _separator + userId;
            return new TurnCredential(username, CreatePassword(username), _realm, userId, expiresAt, expiresAtUnixSeconds);
        }

        /// <summary>
        /// 按用户名派生密码：Base64(HMAC-SHA1(共享密钥, 用户名))，与 coturn 的 use-auth-secret 一致
        /// </summary>
        /// <param name="username">用户名</param>
        /// <returns>Base64 形式的密码</returns>
        /// <exception cref="ArgumentException">username 为空</exception>
        public string CreatePassword(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentException("用户名不能为空", nameof(username));
            }

            using (var hmac = new HMACSHA1(_secret))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(username));
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// 拆分用户名；用于在不知道用户标识时先取出过期时间
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="expiresAtUnixSeconds">过期时间的 Unix 秒数</param>
        /// <param name="userId">用户标识</param>
        /// <returns>格式正确时返回 true</returns>
        public bool TryParseUsername(string username, out long expiresAtUnixSeconds, out string userId)
        {
            expiresAtUnixSeconds = 0;
            userId = null;
            if (string.IsNullOrEmpty(username))
            {
                return false;
            }

            var index = username.IndexOf(_separator, StringComparison.Ordinal);
            if (index <= 0 || index + _separator.Length >= username.Length)
            {
                return false;
            }

            var timestampText = username.Substring(0, index);
            if (!long.TryParse(timestampText, NumberStyles.None, CultureInfo.InvariantCulture, out expiresAtUnixSeconds))
            {
                return false;
            }

            userId = username.Substring(index + _separator.Length);
            return true;
        }

        /// <summary>
        /// 校验第一层凭据：用户名格式、时间戳未过期、密码与派生值一致。
        /// 只要用户名格式正确且未过期，就通过 <paramref name="password"/> 返回派生密码，供第二层派生长期密钥。
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">待校验的密码；为 null 时只做格式与有效期校验</param>
        /// <param name="expiresAtUnixSeconds">用户名里的过期时间，单位秒</param>
        /// <param name="userId">用户名里的用户标识</param>
        /// <returns>通过校验返回 true</returns>
        public bool TryValidate(string username, string password, out long expiresAtUnixSeconds, out string userId)
        {
            if (!TryParseUsername(username, out expiresAtUnixSeconds, out userId))
            {
                return false;
            }

            if (expiresAtUnixSeconds < _clock().ToUnixTimeSeconds())
            {
                return false;
            }

            if (password == null)
            {
                return true;
            }

            return FixedTimeEquals(CreatePassword(username), password);
        }

        /// <summary>
        /// 输出不含共享密钥与密码的摘要，供日志与诊断使用
        /// </summary>
        /// <returns>摘要文本</returns>
        public override string ToString()
        {
            return "TurnCredentialService(域=" + _realm + "，有效期=" + _ttlSeconds.ToString(CultureInfo.InvariantCulture) + "s)";
        }

        /// <summary>
        /// 定长比较两个字符串，避免按字符提前返回泄露信息
        /// </summary>
        /// <param name="expected">期望值</param>
        /// <param name="actual">实际值</param>
        /// <returns>完全一致返回 true</returns>
        private static bool FixedTimeEquals(string expected, string actual)
        {
            if (expected == null || actual == null)
            {
                return false;
            }

            var left = Encoding.UTF8.GetBytes(expected);
            var right = Encoding.UTF8.GetBytes(actual);

            // 长度不同时仍遍历较短一段，避免长度差异通过耗时泄露
            var length = Math.Min(left.Length, right.Length);
            var diff = left.Length ^ right.Length;
            for (var i = 0; i < length; i++)
            {
                diff |= left[i] ^ right[i];
            }
            return diff == 0;
        }
    }
}