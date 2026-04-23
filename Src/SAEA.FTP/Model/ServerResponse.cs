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
*唯一标识：7fdc901a-f2f0-4229-81fd-a2b65db7fef5
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/08 09:18:57
*描述：
*
*=====================================================================
*修改标记
*修改时间：2019/11/08 09:18:57
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.FTP.Model
{
    public class ServerResponse
    {
        public int Code { get; set; }

        public string Reply { get; set; }

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
