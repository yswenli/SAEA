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
*命名空间：SAEA.Socket5.Model
*文件名： ISocks5UserValidator
*版本号： v26.11.5.1
*唯一标识：e0f15e17-1496-491d-a2f4-37a430ce1f11
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/9/20 02:53:58
*描述：SOCKS5 用户名/密码校验器接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 13:11:19
*修改人： yswenli
*版本号： v26.11.5.1
*描述：规范文件头 banner
*
*****************************************************************************/
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
