/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.Http.Model
*文件名： IInvoker
*版本号： V3.6.2.1
*唯一标识：eeefb8e0-9493-4a07-b469-fc24db360a1b
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/8 16:34:03
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/8 16:34:03
*修改人： yswenli
*版本号： V3.6.2.1
*描述：
*
*****************************************************************************/
namespace SAEA.Http.Model
{
    public interface IInvoker
    {
        object Parma { get; set; }

        IHttpResult GetActionResult(HttpContext context);
    }
}