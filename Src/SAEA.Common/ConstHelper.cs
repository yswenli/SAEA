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
*命名空间：SAEA.Common
*文件名： ConstHelper
*版本号： v26.4.23.1
*唯一标识：ff864513-9acd-4a75-8995-0ec5c46e1690
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/09/29 16:58:49
*描述：ConstHelper帮助类
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 18:21:44
*修改人： yswenli
*版本号： v26.11.16.1
*描述：CORS 默认方法清单补 PATCH/HEAD，与 MVC 放行的六谓词对齐
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SAEA.Common
{
    /// <summary>
    /// HTTP 协议与 MVC 框架共用常量
    /// </summary>
    public class ConstHelper
    {
        /// <summary>
        /// 路由中控制器段的占位名
        /// </summary>
        public const string CONTROLLERNAME = "controller";

        /// <summary>
        /// MVC 控制器基类的全名
        /// </summary>
        public const string CONTROLLERFULLNAME = "SAEA.MVC.Controllers.Controller";

        /// <summary>
        /// HttpGet 特性名
        /// </summary>
        public const string HTTPGET = "HttpGet";

        /// <summary>
        /// HttpPost 特性名
        /// </summary>
        public const string HTTPPOST = "HttpPost";

        /// <summary>
        /// HTTP 方法名 GET
        /// </summary>
        public const string GET = "GET";

        /// <summary>
        /// HTTP 方法名 POST
        /// </summary>
        public const string POST = "POST";

        /// <summary>
        /// HTTP 方法名 PUT
        /// </summary>
        public const string PUT = "PUT";

        /// <summary>
        /// HTTP 方法名 DELETE
        /// </summary>
        public const string DELETE = "DELETE";

        /// <summary>
        /// HTTP 方法名 PATCH
        /// </summary>
        public const string PATCH = "PATCH";

        /// <summary>
        /// HTTP 方法名 HEAD，语义与 GET 相同但不返回报文体
        /// </summary>
        public const string HEAD = "HEAD";

        /// <summary>
        /// HTTP 方法名 OPTIONS，用于跨域预检
        /// </summary>
        public const string OPTIONS = "OPTIONS";

        /// <summary>
        /// 表单 url 编码的内容类型
        /// </summary>
        public const string FormUrlEncode = "application/x-www-form-urlencoded";

        /// <summary>
        /// 多段表单的内容类型前缀，实际发送时需拼接 boundary
        /// </summary>
        public const string FormData = "multipart/form-data";

        /// <summary>
        /// json 内容类型，带 UTF-8 字符集
        /// </summary>
        public const string Json = "application/json; charset=UTF-8";

        /// <summary>
        /// 动作执行前过滤器的方法名
        /// </summary>
        public const string ONACTIONEXECUTING = "OnActionExecuting";

        /// <summary>
        /// 动作执行后过滤器的方法名
        /// </summary>
        public const string ONACTIONEXECUTED = "OnActionExecuted";

        /// <summary>
        /// Server 响应头内容的 ASCII 码表，以逗号分隔，运行时解码后拼接版本号
        /// </summary>
        public const string SERVERMVCSERVER = "83,101,114,118,101,114,58,32,83,65,69,65,46,72,116,116,112,46,83,101,114,118,101,114,32";

        /// <summary>
        /// Content-Type 头名
        /// </summary>
        public const string CT = "Content-Type";

        /// <summary>
        /// 多段表单 boundary 参数的前缀，含前导空格
        /// </summary>
        public const string BOUNDARY = " boundary=";

        /// <summary>
        /// 查询参数名 id
        /// </summary>
        public const string ID = "id";

        /// <summary>
        /// 动作过滤器特性基类的名称
        /// </summary>
        public const string ACTIONFILTERATTRIBUTE = "ActionFilterAttribute";

        /// <summary>
        /// 回车换行符，用于拼接 HTTP 头
        /// </summary>
        public const string ENTER = "\r\n";

        /// <summary>
        /// 双回车换行符，用于标记 HTTP 头结束
        /// </summary>
        public const string DENTER = "\r\n\r\n";

        /// <summary>
        /// 空格字符
        /// </summary>
        public const string SPACE = " ";

        /// <summary>
        /// 星号字符，用于通配 Accept 等头值
        /// </summary>
        public const string ASTERRISK = "*";

        /// <summary>
        /// 减号字符
        /// </summary>
        public const string MINUSSIGN = "-";

        /// <summary>
        /// 美元符号
        /// </summary>
        public const string DOLLAR = "$";

        /// <summary>
        /// 冒号，用于分隔 HTTP 头名与值
        /// </summary>
        public const string COLON = ":";

        /// <summary>
        /// 逗号，用于分隔多个头值
        /// </summary>
        public const string COMMA = ",";

        /// <summary>
        /// 正斜杠，用于拼接 URL 路径
        /// </summary>
        public const string SLASH = "/";

        /// <summary>
        /// 小于号
        /// </summary>
        public const string LESS_THAN = "<";

        /// <summary>
        /// 大于号
        /// </summary>
        public const string GREATER_THAN = ">";

        /// <summary>
        /// 和号，用于分隔 URL 查询参数
        /// </summary>
        public const string AMPERSAND = "&";

        /// <summary>
        /// 等号，用于分隔参数名与值
        /// </summary>
        public const string EQUO = "=";

        /// <summary>
        /// 分号
        /// </summary>
        public const string SEMICOLON = ";";

        /// <summary>
        /// 问号，用于标记 URL 查询串起始
        /// </summary>
        public const string QUESTIONMARK = "?";

        /// <summary>
        /// 双引号
        /// </summary>
        public const string DOUBLEQUOTES = "\"";


        static string _serverName = string.Empty;

        /// <summary>
        /// Server 响应头值，由固定内容拼接框架版本号，首次访问后缓存
        /// </summary>
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

        /// <summary>
        /// gzip 内容编码响应头，值固定为 gzip，首次访问后缓存
        /// </summary>
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

        /// <summary>
        /// 跨域响应头内容，含允许的方法、来源与请求头，首次访问后缓存，自定义头变化时重建
        /// </summary>
        public static string CrossDomain
        {
            get
            {
                if (string.IsNullOrEmpty(_crossDomain))
                {
                    var builder = new StringBuilder();
                    //支持跨域
                    builder.AppendLine("Access-Control-Allow-Methods: GET, HEAD, POST, PUT, DELETE, PATCH, OPTIONS");
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
                    builder.AppendLine("Access-Control-Request-Methods: GET, HEAD, POST, PUT, DELETE, PATCH, OPTIONS");
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





        /// <summary>
        /// 会话标识使用的 Cookie 名称
        /// </summary>
        public const string SESSIONID = "SAEA.HttpServer.SessionID";


        /// <summary>
        /// 输出缓存特性的全名
        /// </summary>
        public const string OUTPUTCACHEATTRIBUTE = "SAEA.MVC.Attributes.OutputCacheAttribute";




    }
}