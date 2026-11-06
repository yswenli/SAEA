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
*文件名： IHttpResult
*版本号： v26.4.23.1
*唯一标识：5e159b52-ab34-41ae-ba59-c48200b3c0db
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/29 15:01:17
*描述：IHttpResult接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:29
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Common.Newtonsoft.Json;
using System.Net;
using System.Text;

namespace SAEA.Http.Model
{
    /// <summary>
    /// 公共返回的接口
    /// </summary>
    public interface IHttpResult
    {
        /// <summary>
        /// 报文体字节
        /// </summary>
        byte[] Content { get; set; }

        /// <summary>
        /// 报文体编码
        /// </summary>
        [JsonIgnore]
        Encoding ContentEncoding { get; set; }
        /// <summary>
        /// 报文体内容类型
        /// </summary>
        string ContentType { get; set; }
        /// <summary>
        /// 响应状态码
        /// </summary>
        HttpStatusCode Status { get; set; }
    }
}