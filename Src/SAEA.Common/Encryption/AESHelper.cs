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
*命名空间：SAEA.Common.Encryption
*文件名： AESHelper
*版本号： v26.4.23.1
*唯一标识：43e3cf96-8672-49a2-927e-bac5d242f3f3
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/16 17:50:24
*描述：AESHelper帮助类
*
*=====================================================================
*修改标记
*修改时间：2020/12/16 17:50:24
*修改人： yswenli
*版本号： v26.4.23.1
*描述：AESHelper帮助类
*
*****************************************************************************/
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SAEA.Common.Encryption
{
    /// <summary>
    ///     AES对称加密算法类
    /// </summary>
    public class AESHelper
    {
        /// <summary>
        /// AES加密
        /// </summary>
        /// <param name="encryptString"></param>
        /// <param name="encryptKey"></param>
        /// <param name="isbase64"></param>
        /// <returns></returns>
        public static string Encrypt(string encryptString, string encryptKey, bool isbase64 = true)
        {
            string returnValue;
            var temp = Convert.FromBase64String("Rkb4jvUy/ye7Cd7k89QQgQ==");
            var AESProvider = Rijndael.Create();
            try
            {
                var defaultKey = "3B2hb2oYHpmZrFflfdmSon1x";
                if (string.IsNullOrEmpty(encryptKey))
                    encryptKey = defaultKey;
                if (encryptKey.Length < 24)
                    encryptKey = encryptKey + defaultKey.Substring(0, 24 - encryptKey.Length);
                if (encryptKey.Length > 24)
                    encryptKey = encryptKey.Substring(0, 24);
                var byteEncryptString = Encoding.UTF8.GetBytes(encryptString);
                using (var memoryStream = new MemoryStream())
                {
                    using (
                        var cryptoStream = new CryptoStream(memoryStream,
                            AESProvider.CreateEncryptor(Encoding.UTF8.GetBytes(encryptKey), temp),
                            CryptoStreamMode.Write))
                    {
                        cryptoStream.Write(byteEncryptString, 0, byteEncryptString.Length);
                        cryptoStream.FlushFinalBlock();
                        if (isbase64)
                            returnValue = Convert.ToBase64String(memoryStream.ToArray());
                        else
                            returnValue = memoryStream.ToArray().ByteToHexStr();
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return returnValue;
        }

        /// <summary>
        /// AES 解密
        /// </summary>
        /// <param name="decryptString"></param>
        /// <param name="decryptKey"></param>
        /// <param name="isbase64"></param>
        /// <returns></returns>
        public static string Decrypt(string decryptString, string decryptKey, bool isbase64 = true)
        {
            var returnValue = "";
            var temp = Convert.FromBase64String("Rkb4jvUy/ye7Cd7k89QQgQ==");
            var AESProvider = Rijndael.Create();
            try
            {
                var defaultKey = "3B2hb2oYHpmZrFflfdmSon1x";
                if (string.IsNullOrEmpty(decryptKey))
                    decryptKey = defaultKey;
                if (decryptKey.Length < 24)
                    decryptKey = decryptKey + defaultKey.Substring(0, 24 - decryptKey.Length);
                if (decryptKey.Length > 24)
                    decryptKey = decryptKey.Substring(0, 24);
                byte[] byteDecryptString = null;
                if (isbase64)
                {
                    byteDecryptString = Convert.FromBase64String(decryptString);
                }
                else
                {
                    byteDecryptString = decryptString.StrToHexByte();
                }
                using (var memoryStream = new MemoryStream())
                {
                    using (
                        var cryptoStream = new CryptoStream(memoryStream,
                            AESProvider.CreateDecryptor(Encoding.UTF8.GetBytes(decryptKey), temp),
                            CryptoStreamMode.Write))
                    {
                        cryptoStream.Write(byteDecryptString, 0, byteDecryptString.Length);
                        cryptoStream.FlushFinalBlock();
                        returnValue = Encoding.UTF8.GetString(memoryStream.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return returnValue;
        }
    }
}
