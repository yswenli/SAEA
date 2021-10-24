/****************************************************************************
*Copyright (c) 2018-2021yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MVC
*文件名： ActionResult
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
using SAEA.Http.Model;
using System.Net;
using System.Runtime.Serialization;
using System.Text;

namespace SAEA.MVC
{
    /// <summary>
    /// 封装一个操作方法的结果并用于代表该操作方法执行框架级操作。
    /// </summary>
    [DataContract]
    public abstract class ActionResult : IHttpResult
    {
        /// <summary>
        ///  初始化 SAEA.Http.ActionResult 类的新实例。
        /// </summary>
        public ActionResult()
        {

        }

        /// <summary>
        /// 获取或设置内容。
        /// </summary>
        [DataMember]
        public byte[] Content { get; set; }

        /// <summary>
        /// 获取或设置内容编码。
        /// </summary>
        public Encoding ContentEncoding { get; set; } = Encoding.UTF8;

        /// <summary>
        /// 获取或设置内容的类型。
        /// </summary>
        [DataMember]
        public string ContentType { get; set; } = "application/json";

        /// <summary>
        /// 状态码
        /// </summary>
        [DataMember]
        public HttpStatusCode Status
        {
            get; set;
        } = HttpStatusCode.OK;


    }
}
