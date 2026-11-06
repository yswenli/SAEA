/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.P2P.Security
*文件名： KeyExchange
*版本号： v26.4.23.1
*唯一标识：c15a807c-b468-4d26-8399-3b2b44e13c34
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 16:04:05
*描述：KeyExchange交换类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:20:16
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;

namespace SAEA.P2P.Security
{
    /// <summary>
    /// 会话密钥持有者：默认随机生成 128 位密钥，也可由外部共享密钥构造
    /// </summary>
    public class KeyExchange
    {
        /// <summary>
        /// 会话密钥；随机生成时为 32 位小写十六进制串，由共享密钥构造时原样保留外部值
        /// </summary>
        public string SessionKey { get; private set; }
        /// <summary>
        /// 密钥生成时间，UTC
        /// </summary>
        public DateTime CreatedTime { get; private set; }
        /// <summary>
        /// 密钥是否处于可用状态，构造后默认为 true
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// 创建实例并随机生成会话密钥，同时置为可用
        /// </summary>
        public KeyExchange()
        {
            SessionKey = GenerateSessionKey();
            CreatedTime = DateTime.UtcNow;
            IsActive = true;
        }

        /// <summary>
        /// 创建新的随机会话密钥实例
        /// </summary>
        /// <returns>已生成密钥并处于可用状态的实例</returns>
        public static KeyExchange Create()
        {
            return new KeyExchange();
        }

        /// <summary>
        /// 用外部共享密钥构造实例，会先用随机密钥初始化再覆盖为传入值
        /// </summary>
        /// <param name="sharedKey">外部共享密钥，原样作为会话密钥，不做校验</param>
        /// <returns>会话密钥为 sharedKey、处于可用状态的实例</returns>
        public static KeyExchange FromSharedKey(string sharedKey)
        {
            var ke = new KeyExchange();
            ke.SessionKey = sharedKey;
            return ke;
        }

        private static string GenerateSessionKey()
        {
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                var bytes = new byte[16];
                rng.GetBytes(bytes);
                return BitConverter.ToString(bytes).Replace("-", "").ToLower();
            }
        }

        /// <summary>
        /// 将密钥置为不可用，不清理 SessionKey
        /// </summary>
        public void Deactivate()
        {
            IsActive = false;
        }
    }
}