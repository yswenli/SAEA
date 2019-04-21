/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MVC
*文件名： IFilter
*版本号： v4.5.1.2
*唯一标识：a22caf84-4c61-456e-98cc-cbb6cb2c6d6e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/11 13:39:02
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/11 13:39:02
*修改人： yswenli
*版本号： v4.5.1.2
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.MVC
{
    public interface IFilter
    {
        /// <summary>
        /// 执行顺序
        /// </summary>
        int Order { get; set; }
    }
}
