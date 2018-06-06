/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.WebAPI.Mvc
*文件名： ActionResult
*版本号： V1.0.0.0
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
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using System.Net;
using System.Text;

namespace SAEA.WebAPI.Mvc
{
    // 摘要:
    //     封装一个操作方法的结果并用于代表该操作方法执行框架级操作。
    public abstract class ActionResult
    {
        // 摘要:
        //     初始化 SAEA.WebAPI.ActionResult 类的新实例。
        public ActionResult()
        {

        }

        // 摘要:
        //     获取或设置内容。
        //
        // 返回结果:
        //     内容。
        public string Content { get; set; }
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
