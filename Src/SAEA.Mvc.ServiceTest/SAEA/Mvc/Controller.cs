/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MVC
*文件名： Controller
*版本号： v4.3.2.5
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
*版本号： v4.3.2.5
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Http;
using SAEA.Http.Model;

namespace SAEA.MVC
{
    /// <summary>
    /// MVC控制器
    /// </summary>
    public abstract class Controller
    {
        public HttpContext HttpContext { get; set; }

        /// <summary>
        /// 返回Json
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        protected JsonResult Json(object data, bool expended = false)
        {
            return new JsonResult(data, expended);
        }
        /// <summary>
        /// 自定义内容
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        protected ContentResult Content(string data)
        {
            return new ContentResult(data);
        }


        /// <summary>
        /// 小文件
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        protected FileResult File(string filePath)
        {
            return new FileResult(filePath, HttpContext.IsStaticsCached);
        }


        /// <summary>
        /// 空结果
        /// </summary>
        /// <returns></returns>
        protected EmptyResult Empty()
        {
            return new EmptyResult();
        }

        protected string Serialize<T>(T t, bool expended = false)
        {
            return SerializeHelper.Serialize(t, expended);
        }

        protected T Deserialize<T>(string json)
        {
            return SerializeHelper.Deserialize<T>(json);
        }

        /// <summary>
        /// 判断客户端请求是否为ajax
        /// </summary>
        /// <returns></returns>
        protected bool IsAjaxRequest()
        {
            if (HttpContext.Request.Headers.ContainsKey("X-Requested-With"))
            {
                return true;
            }
            return false;
        }
    }
}
