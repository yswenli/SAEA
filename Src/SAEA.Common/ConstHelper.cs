﻿/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MVC.Mvc
*文件名： AreaCollection
*版本号： v7.0.0.1
*唯一标识：eb956356-8ea4-4657-aec1-458a3654c078
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/8/2 18:10:16
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/8/2 18:10:16
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SAEA.Common
{
    public class ConstHelper
    {
        public const string CONTROLLERNAME = "controller";

        public const string CONTROLLERFULLNAME = "SAEA.MVC.Controller";

        public const string HTTPGET = "HttpGet";

        public const string HTTPPOST = "HttpPost";

        public const string GET = "GET";

        public const string POST = "POST";

        public const string PUT = "PUT";

        public const string DELETE = "DELETE";

        public const string OPTIONS = "OPTIONS";

        /// <summary>
        /// application/x-www-form-urlencoded
        /// </summary>
        public const string FormUrlEncode = "application/x-www-form-urlencoded";

        /// <summary>
        /// multipart/form-data
        /// </summary>
        public const string FormData = "multipart/form-data";

        /// <summary>
        /// application/json
        /// </summary>
        public const string Json = "application/json; charset=UTF-8";

        public const string ONACTIONEXECUTING = "OnActionExecuting";

        public const string ONACTIONEXECUTED = "OnActionExecuted";

        public const string SERVERMVCSERVER = "83,101,114,118,101,114,58,32,83,65,69,65,46,72,116,116,112,46,83,101,114,118,101,114,32";

        public const string CT = "Content-Type";

        public const string BOUNDARY = " boundary=";

        public const string ID = "id";

        /// <summary>
        /// ActionFilterAttribute
        /// </summary>
        public const string ACTIONFILTERATTRIBUTE = "ActionFilterAttribute";

        public const string ENTER = "\r\n";

        public const string DENTER = "\r\n\r\n";

        public const string SPACE = " ";

        public const string ASTERRISK = "*";

        public const string MINUSSIGN = "-";

        public const string DOLLAR = "$";

        public const string COLON = ":";

        public const string COMMA = ",";

        public const string SLASH = "/";

        public const string LESS_THAN = "<";

        public const string GREATER_THAN = ">";

        public const string AMPERSAND = "&";

        public const string EQUO = "=";

        public const string SEMICOLON = ";";

        public const string QUESTIONMARK = "?";

        public const string DOUBLEQUOTES = "\"";


        static string _serverName = string.Empty;

        public static string ServerName
        {
            get
            {
                if (string.IsNullOrEmpty(_serverName))
                {
                    _serverName = $"{Encoding.ASCII.GetString(SERVERMVCSERVER.Split(",").Select(b => Convert.ToByte(b)).ToArray())}{SAEAVersion.ToString()}";
                }
                return _serverName;
            }
        }


        static string _contentEncoding = string.Empty;

        public static string ContentEncoding
        {
            get
            {
                if (string.IsNullOrEmpty(_contentEncoding))
                {
                    _contentEncoding = "Content-Encoding: gzip";
                }
                return _contentEncoding;
            }
        }


        #region CrossDomain

        static string _crossDomain = string.Empty;

        public static string CrossDomain
        {
            get
            {
                if (string.IsNullOrEmpty(_crossDomain))
                {
                    var builder = new StringBuilder();
                    //支持跨域
                    builder.AppendLine("Access-Control-Allow-Methods: GET, POST, PUT, DELETE, OPTIONS");
                    builder.AppendLine("Access-Control-Allow-Origin: *");
                    builder.Append("Access-Control-Allow-Headers: Content-Type,X-Requested-With,Accept,Authorization,token,yswenli");//可自行增加额外的header
                    if (_crossDomainHeaders.Any())
                    {
                        foreach (var item in _crossDomainHeaders)
                        {
                            if (!string.IsNullOrWhiteSpace(item))
                                builder.Append($",{item}");
                        }
                    }
                    builder.AppendLine();
                    builder.AppendLine("Access-Control-Request-Methods: GET, POST, PUT, DELETE, OPTIONS");
                    _crossDomain = builder.ToString();
                }
                return _crossDomain;
            }
        }

        static List<string> _crossDomainHeaders = new List<string>();

        /// <summary>
        /// 设置需要跨域的自定义headers
        /// </summary>
        /// <param name="headers"></param>
        public static void SetCrossDomainHeaders(IEnumerable<string> headers)
        {
            if (headers != null && headers.Any())
            {
                _crossDomainHeaders = headers.ToList();
                _crossDomain = string.Empty;
            }
        }

        /// <summary>
        /// 设置需要跨域的自定义headers
        /// </summary>
        /// <param name="headers"></param>
        public static void SetCrossDomainHeaders(string headers)
        {
            if (!string.IsNullOrEmpty(headers) && headers.Contains(","))
            {
                var arr = headers.Split(",", StringSplitOptions.RemoveEmptyEntries);
                if (arr != null && arr.Any())
                {
                    SetCrossDomainHeaders(arr);
                }
            }
        }

        #endregion





        public const string SESSIONID = "SAEA.HttpServer.SessionID";


        public const string OUTPUTCACHEATTRIBUTE = "SAEA.MVC.OutputCacheAttribute";




    }
}
