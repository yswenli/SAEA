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
*命名空间：SAEA.Http.Model
*文件名： HttpActionResult
*版本号： v26.4.23.1
*唯一标识：cddb3d49-ffa2-4724-9469-018fb0597794
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/29 15:01:17
*描述：HttpActionResult接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:29
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System.Net;
using System.Text;

namespace SAEA.Http.Model
{
    /// <summary>
    /// 操作方法结果的基类，承载报文体、编码、内容类型与状态码
    /// </summary>
    public abstract class HttpActionResult : IHttpResult
    {
        /// <summary>
        /// 初始化结果对象，编码默认 UTF-8，内容类型默认为 application/json
        /// </summary>
        public HttpActionResult()
        {

        }

        /// <summary>
        /// 获取或设置内容
        /// </summary>
        public byte[] Content { get; set; }
        /// <summary>
        /// 获取或设置内容编码，默认 UTF-8
        /// </summary>
        public Encoding ContentEncoding { get; set; } = Encoding.UTF8;
        /// <summary>
        /// 获取或设置内容的类型，默认 application/json
        /// </summary>
        public string ContentType { get; set; } = "application/json";


        /// <summary>
        /// 响应状态码，默认 200
        /// </summary>
        public HttpStatusCode Status
        {
            get; set;
        } = HttpStatusCode.OK;


    }
}