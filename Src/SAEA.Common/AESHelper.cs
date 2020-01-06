/****************************************************************************
*项目名称：SAEA.Common
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Common
*类 名 称：AESHelper
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/1/6 13:31:51
*描述：
*=====================================================================
*修改时间：2020/1/6 13:31:51
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SAEA.Common
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
            var temp = Convert.FromBase64String("eXN3ZW5saQ==");
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
            var temp = Convert.FromBase64String("eXN3ZW5saQ==");
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
