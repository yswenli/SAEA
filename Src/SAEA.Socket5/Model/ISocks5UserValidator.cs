/* SAEA.Socket5
 * 命名空间：SAEA.Socket5.Model
 * 文件名：ISocks5UserValidator.cs
 * 版本号：v26.9.20.1
 * 描述：SOCKS5 用户名/密码校验器接口（RFC1929）
 */

namespace SAEA.Socket5.Model
{
    /// <summary>
    /// SOCKS5 用户名/密码校验器接口
    /// </summary>
    public interface ISocks5UserValidator
    {
        /// <summary>
        /// 校验用户名与密码
        /// </summary>
        /// <param name="userName">用户名</param>
        /// <param name="password">密码</param>
        /// <returns>校验通过返回 true</returns>
        bool Validate(string userName, string password);
    }
}
