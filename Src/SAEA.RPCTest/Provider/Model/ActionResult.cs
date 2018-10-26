/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：IM.RPC.Test.Providers.Model
*文件名： JsonResult
*版本号： V2.2.2.1
*唯一标识：93cb887f-eaf3-406e-a3b6-1c23b7b6dd15
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/6/11 9:24:36
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/6/11 9:24:36
*修改人： yswenli
*版本号： V2.2.2.1
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SAEA.RPCTest.Providers.Model
{
    public class ActionResult<T>
    {
        public bool Success
        {
            get; set;
        }

        public string Error
        {
            get; set;
        }

        public int Code
        {
            get; set;
        }

        public T Data
        {
            get; set;
        }
    }
}
