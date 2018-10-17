/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.MVC.Base
*文件名： HttpServerUtilityBase
*版本号： V2.2.0.1
*唯一标识：01f783cd-c751-47c5-a5b9-96d3aa840c70
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/16 11:03:29
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/16 11:03:29
*修改人： yswenli
*版本号： V2.2.0.1
*描述：
*
*****************************************************************************/
using System;
using System.IO;

namespace SAEA.MVC.Base
{
    /// <summary>
    /// HttpUtility
    /// .core
    /// </summary>
    public class HttpUtility
    {
        const string Controller = "Controller";

        string _root = "/";

        Uri _prex;


        public HttpUtility(string root)
        {
            _root = root;
            _prex = new Uri("http://127.0.0.1:39654");
        }

        /// <summary>
        /// MapPath
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public virtual string MapPath(string path)
        {
            var uri = new Uri(_prex, _root + path);
           
            return Path.GetFullPath(Directory.GetCurrentDirectory() + uri.LocalPath); 
        }
        /// <summary>
        /// UrlEncode
        /// </summary>
        /// <param name="s"></param>
        /// <param name="isData"></param>
        /// <returns></returns>
        public static string UrlEncode(string s, bool isData = false)
        {
            if (isData)
            {
                return Uri.EscapeDataString(s);
            }
            return Uri.EscapeUriString(s);

        }
        /// <summary>
        /// UrlDecode
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string UrlDecode(string s)
        {
            return Uri.UnescapeDataString(s);
        }

        /// <summary>
        /// HtmlEncode
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string HtmlEncode(string str)
        {
            return System.Web.HttpUtility.HtmlEncode(str);
        }
        /// <summary>
        /// HtmlDecode
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        public static string HtmlDecode(string html)
        {
            return System.Web.HttpUtility.HtmlDecode(html);
        }
    }
}
