/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RedisSocket
*文件名： RedisConfig
*版本号： V1.0.0.0
*唯一标识：95beca71-860b-4c09-b655-17c7d923f8a3
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/3 17:29:13
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/3 17:29:13
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RedisSocket
{
    [Serializable]
    public class RedisConfig
    {
        public string IP
        {
            get; set;
        }
        public int Port
        {
            get; set;
        }
        public string Passwords
        {
            get; set;
        }

        public RedisConfig()
        {

        }

        public RedisConfig(string ipPort, string passwords)
        {
            try
            {
                var IPPort = ipPort.GetIPPort();
                this.IP = IPPort.Item1;
                this.Port = IPPort.Item2;
                this.Passwords = passwords;
            }
            catch (Exception ex)
            {
                throw new Exception("连接字符串格式有误，例如格式为：server=127.0.0.1:6379;passwords=yswenli; error:" + ex.Message);
            }
        }

        /// <summary>
        /// connectionStr
        /// 格式为：server=127.0.0.1:6379;passwords=yswenli
        /// </summary>
        /// <param name="connectionStr"></param>
        public RedisConfig(string connectionStr)
        {
            try
            {
                var arr = connectionStr.Split(";", StringSplitOptions.RemoveEmptyEntries);
                foreach (var item in arr)
                {
                    if (item.Contains("server="))
                    {
                        var IPPort = item.Split("=", StringSplitOptions.RemoveEmptyEntries)[1].GetIPPort();
                        this.IP = IPPort.Item1;
                        this.Port = IPPort.Item2;
                    }
                    else if (item.Contains("passwords="))
                    {
                        this.Passwords = item.Split("=", StringSplitOptions.RemoveEmptyEntries)[1];
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("连接字符串格式有误，例如格式为：server=127.0.0.1:6379;passwords=yswenli; error:" + ex.Message);
            }
        }
        /// <summary>
        /// 将当前实体输出为连接字符串
        /// </summary>
        /// <returns></returns>
        public new string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("server=");
            sb.Append(this.IP);
            sb.Append(":");
            sb.Append(this.Port);
            sb.Append(";");
            sb.Append("passwords=");
            sb.Append(this.Passwords);
            return sb.ToString();
        }

        public string GetIPPort()
        {
            return this.IP + ":" + this.Port;
        }

    }
}
