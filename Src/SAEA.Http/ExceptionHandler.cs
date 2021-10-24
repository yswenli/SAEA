/****************************************************************************
*项目名称：SAEA.MVC
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.MVC
*类 名 称：ExceptionHandlerAttribute
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/7/29 15:44:32
*描述：
*=====================================================================
*修改时间：2020/7/29 15:44:32
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using SAEA.Http.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.Http
{
    /// <summary>
    /// 未拦截异常委托
    /// </summary>
    /// <param name="httpContext"></param>
    /// <param name="ex"></param>
    /// <returns></returns>
    public delegate IHttpResult ExceptionHandler(IHttpContext httpContext, Exception ex);
}
