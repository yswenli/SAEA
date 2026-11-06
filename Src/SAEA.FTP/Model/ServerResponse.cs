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
*命名空间：SAEA.FTP.Model
*文件名： ServerResponse
*版本号： v26.4.23.1
*唯一标识：e3ed7701-d28f-44c8-a21c-e0210daf952b
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/08 09:18:57
*描述：ServerResponse类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:28
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.FTP.Model
{
    /// <summary>
    /// 服务器单行响应，包含三位状态码与响应文本
    /// </summary>
    public class ServerResponse
    {
        /// <summary>
        /// 三位响应码，取值含义见 <see cref="ServerResponseCode"/>
        /// </summary>
        public int Code { get; set; }

        /// <summary>
        /// 响应文本，即状态码与其后空格之后的内容
        /// </summary>
        public string Reply { get; set; }

        /// <summary>
        /// 解析一行服务器响应，按前三字符取状态码、第五字符起取文本
        /// </summary>
        /// <param name="str">一行原始响应文本</param>
        /// <returns>解析后的响应对象</returns>
        public static ServerResponse Parse(string str)
        {
            try
            {
                return new ServerResponse()
                {
                    Code = int.Parse(str.Substring(0, 3)),
                    Reply = str.Substring(4)
                };
            }
            catch
            {
                throw new Exception("ServerResponse Parse Failed,str:" + str);
            }
        }
    }
}
