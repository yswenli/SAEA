/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.Commom
*文件名： MD5Helper
*版本号： V1.0.0.0
*唯一标识：f52cdcce-5ea8-4704-b8dd-3ab1e1e23131
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/18 14:30:10
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/18 14:30:10
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SAEA.Common
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
    }
}
