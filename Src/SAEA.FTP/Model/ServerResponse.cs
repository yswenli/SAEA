/****************************************************************************
*项目名称：SAEA.FTP.Model
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.FTP.Model
*类 名 称：ServerResponse
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/7 14:00:28
*描述：
*=====================================================================
*修改时间：2019/11/7 14:00:28
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
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
