/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MVCTest.Model
*文件名： UserInfo
*版本号： v4.2.1.6
*唯一标识：87527f7a-54bc-4833-b04b-c5ba8551f048
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/8 10:45:16
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/8 10:45:16
*修改人： yswenli
*版本号： v4.2.1.6
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.MVCTest.Model
{
    public class UserInfo
    {
        public int ID { get; set; }

        public string UserName { get; set; }

        public string NickName { get; set; }
    }
}
