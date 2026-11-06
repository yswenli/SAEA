/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：IM.RPC.Test.Providers.Model
*文件名： JsonResult
*版本号： v7.0.0.1
*唯一标识：93cb887f-eaf3-406e-a3b6-1c23b7b6dd15
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/6/11 9:24:36
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/6/11 9:24:36
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SAEA.RPCTest.Providers.Model
{
    /// <summary>
    /// 统一返回结果包装，承载成功标志、错误信息、状态码与业务数据
    /// </summary>
    /// <typeparam name="T">业务数据类型</typeparam>
    public class ActionResult<T>
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success
        {
            get; set;
        }

        /// <summary>
        /// 失败时的错误描述，成功时为 null
        /// </summary>
        public string Error
        {
            get; set;
        }

        /// <summary>
        /// 结果状态码
        /// </summary>
        public int Code
        {
            get; set;
        }

        /// <summary>
        /// 业务数据
        /// </summary>
        public T Data
        {
            get; set;
        }
    }
}
