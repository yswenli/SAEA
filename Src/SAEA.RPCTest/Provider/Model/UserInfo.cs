/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RPCTest.Provider.Model
*文件名： UserInfo
*版本号： V2.2.0.0
*唯一标识：ee55b630-1b26-49c3-a196-8ceeb89dcccf
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/17 19:18:55
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/17 19:18:55
*修改人： yswenli
*版本号： V2.2.0.0
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RPCTest.Provider.Model
{
    public class UserInfo
    {
        public int ID { get; set; }

        public string UserName { get; set; }
        
        public DateTime Birthday { get; set; }
            
    }
}
