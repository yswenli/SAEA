/* SAEA.Socket5
 * 命名空间：SAEA.Socket5.Model
 * 文件名：DefaultUserValidator.cs
 * 版本号：v26.9.20.1
 * 描述：SOCKS5 默认用户名/密码校验器（RFC1929）
 */

using System;
using System.Collections.Generic;

namespace SAEA.Socket5.Model
{
    /// <summary>
    /// 默认 SOCKS5 用户名/密码校验器
    /// </summary>
    public class DefaultUserValidator : ISocks5UserValidator
    {
        private readonly Dictionary<string, string> _users;

        private readonly Func<string, string, bool> _validator;

        /// <summary>
        /// 构造一个拒绝所有凭据的校验器
        /// </summary>
        public DefaultUserValidator()
        {
            _users = new Dictionary<string, string>();
            _validator = null;
        }

        /// <summary>
        /// 基于用户名/密码字典构造校验器
        /// </summary>
        /// <param name="users">用户名到密码的映射</param>
        public DefaultUserValidator(IDictionary<string, string> users)
        {
            _users = new Dictionary<string, string>(users);
            _validator = null;
        }

        /// <summary>
        /// 基于自定义委托构造校验器
        /// </summary>
        /// <param name="validator">自定义校验逻辑</param>
        public DefaultUserValidator(Func<string, string, bool> validator)
        {
            _users = new Dictionary<string, string>();
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        }

        /// <summary>
        /// 添加/更新一个用户
        /// </summary>
        public void AddUser(string userName, string password)
        {
            _users[userName] = password;
        }

        /// <inheritdoc />
        public bool Validate(string userName, string password)
        {
            if (_validator != null)
            {
                return _validator(userName, password);
            }

            // userName 为空时不应视为有效用户，同时避免 Dictionary.TryGetValue(null) 抛异常
            if (string.IsNullOrEmpty(userName))
            {
                return false;
            }

            if (_users.TryGetValue(userName, out var expected))
            {
                return expected == password;
            }

            return false;
        }
    }
}
