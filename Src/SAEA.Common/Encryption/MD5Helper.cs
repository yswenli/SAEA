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
*文件名： MD5Helper
*版本号： v26.4.23.1
*唯一标识：fd171630-01a7-4cba-a3a3-5e63c0fd16d4
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/16 17:50:24
*描述：
*
*=====================================================================
*修改标记
*修改时间：2020/12/16 17:50:24
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SAEA.Common.Encryption
{
    public static class MD5Helper
    {
        /// <summary>
        /// 获取md5Hash
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static string GetMD5(Stream stream)
        {
            using (MD5 md5 = new MD5CryptoServiceProvider())
            {
                var retVal = md5.ComputeHash(stream);
                stream.Close();
                var str = BitConverter.ToString(retVal);
                return str.Replace("-", "");
            }
        }

        /// <summary>
        /// 获取文件md5Hash
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static string GetFileMD5(string fileName)
        {
            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                return GetMD5(fs);
            }
        }

        /// <summary>
        /// 获取md5code
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string GetMD5(byte[] data)
        {
            using (MD5 md5 = new MD5CryptoServiceProvider())
            {
                byte[] retVal = md5.ComputeHash(data);
                var str = System.BitConverter.ToString(retVal);
                return str.Replace("-", "");
            }
        }
        /// <summary>
        /// 获取md5code
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string GetMD5(string str)
        {
            return GetMD5(Encoding.UTF8.GetBytes(str));
        }

        /// <summary>
        /// HmacMD5
        /// </summary>
        /// <param name="key"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        public static string HmacMD5(string key, string source)
        {
            HMACMD5 hmacmd = new HMACMD5(Encoding.Default.GetBytes(key));

            byte[] inArray = hmacmd.ComputeHash(Encoding.Default.GetBytes(source));

            StringBuilder sb = new StringBuilder(32);

            for (int i = 0; i < inArray.Length; i++)
            {
                sb.Append(inArray[i].ToString("x").PadLeft(2, '0'));
            }
            return sb.ToString();
        }
    }
}