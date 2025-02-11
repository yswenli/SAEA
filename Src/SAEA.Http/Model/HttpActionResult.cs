﻿/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http.Model
*文件名： HttpActionResult
*版本号： v7.0.0.1
*唯一标识：39ffb9e8-5bff-4535-9b15-8d744bc100d9
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/10 16:44:33
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/10 16:44:33
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using System.Net;
using System.Text;

namespace SAEA.Http.Model
{
    // 摘要:
    //     封装一个操作方法的结果并用于代表该操作方法执行框架级操作。
    public abstract class HttpActionResult : IHttpResult
    {
        // 摘要:
        //     初始化 SAEA.Http.ActionResult 类的新实例。
        public HttpActionResult()
        {

        }

        // 摘要:
        //     获取或设置内容。
        //
        // 返回结果:
        //     内容。
        public byte[] Content { get; set; }
        //
        // 摘要:
        //     获取或设置内容编码。
        //
        // 返回结果:
        //     内容编码。
        public Encoding ContentEncoding { get; set; } = Encoding.UTF8;
        //
        // 摘要:
        //     获取或设置内容的类型。
        //
        // 返回结果:
        //     内容的类型。
        public string ContentType { get; set; } = "application/json";


        public HttpStatusCode Status
        {
            get; set;
        } = HttpStatusCode.OK;


    }
}
