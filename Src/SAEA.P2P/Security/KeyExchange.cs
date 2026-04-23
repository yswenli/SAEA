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
*唯一标识：57ee003f-7b9f-40e0-a9ea-0930e842fa0b
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/04/20 16:04:05
*描述：
*
*=====================================================================
*修改标记
*修改时间：2026/04/20 16:04:05
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;

namespace SAEA.P2P.Security
{
    public class KeyExchange
    {
        public string SessionKey { get; private set; }
        public DateTime CreatedTime { get; private set; }
        public bool IsActive { get; private set; }

        public KeyExchange()
        {
            SessionKey = GenerateSessionKey();
            CreatedTime = DateTime.UtcNow;
            IsActive = true;
        }

        public static KeyExchange Create()
        {
            return new KeyExchange();
        }

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

        public void Deactivate()
        {
            IsActive = false;
        }
    }
}