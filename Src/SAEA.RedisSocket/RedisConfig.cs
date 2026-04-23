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
*命名空间：SAEA.RedisSocket
*文件名： RedisConfig
*版本号： v26.4.23.1
*唯一标识：2cf40002-2dae-450d-ac5a-302d6a7d40cd
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/04/04 16:14:42
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/04/04 16:14:42
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using System;
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

        /// <summary>
        /// 操作超时
        /// </summary>
        public int ActionTimeOut
        {
            get; set;
        } = 6 * 1000;


        public RedisConfig(string ipPort, string passwords, int actionTimeOut = 6 * 1000)
        {
            try
            {
                var IPPort = ipPort.ToIPPort();
                this.IP = IPPort.Item1;
                this.Port = IPPort.Item2;
                this.Passwords = passwords;
                this.ActionTimeOut = actionTimeOut;
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
                        var IPPort = item.Split("=", StringSplitOptions.RemoveEmptyEntries)[1].ToIPPort();
                        this.IP = IPPort.Item1;
                        this.Port = IPPort.Item2;
                    }
                    else if (item.Contains("passwords="))
                    {
                        this.Passwords = item.Split("=", StringSplitOptions.RemoveEmptyEntries)[1];
                    }
                    else if (item.Contains("actionTimeout="))
                    {
                        this.ActionTimeOut = int.Parse(item.Split("=", StringSplitOptions.RemoveEmptyEntries)[2]);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("连接字符串格式有误，例如格式为：server=127.0.0.1:6379;passwords=yswenli;actionTimeout=6000; error:" + ex.Message);
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
            sb.Append("actionTimeout=");
            sb.Append(this.ActionTimeOut);
            return sb.ToString();
        }

        public string GetIPPort()
        {
            return this.IP + ":" + this.Port;
        }

    }
}