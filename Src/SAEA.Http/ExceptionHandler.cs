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
*命名空间：SAEA.Http
*文件名： ExceptionHandler
*版本号： v26.4.23.1
*唯一标识：c1632a06-08fe-489d-8f61-43c6bc1158c8
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/07/29 16:47:45
*描述：ExceptionHandler接口
*
*=====================================================================
*修改标记
*修改时间：2020/07/29 16:47:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：ExceptionHandler接口
*
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
