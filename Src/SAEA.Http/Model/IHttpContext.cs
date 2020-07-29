/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http.Model
*文件名： IHttpContext
*版本号： v5.0.0.1
*唯一标识：a303db7d-f83c-4c49-9804-032ec2236232
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/10 13:58:08
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/10 13:58:08
*修改人： yswenli
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Sockets.Interface;

namespace SAEA.Http.Model
{
    public interface IHttpContext
    {
        bool IsStaticsCached { get; set; }
        HttpRequest Request { get; }
        HttpResponse Response { get; }
        HttpUtility Server { get; }
        HttpSession Session { get; }
        WebConfig WebConfig { get; set; }

        /// <summary>
        /// 自定义异常事件
        /// </summary>
        event ExceptionHandler OnException;

        void HttpHandle(IUserToken userToken);

        IHttpResult GetActionResult();
    }
}