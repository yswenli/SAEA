﻿/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http.Model
*文件名： IHttpResult
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
        byte[] Content { get; set; }

        [JsonIgnore]
        Encoding ContentEncoding { get; set; }
        string ContentType { get; set; }
        HttpStatusCode Status { get; set; }
    }
}