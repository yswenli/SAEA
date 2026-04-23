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
*命名空间：SAEA.MVC
*文件名： Controller
*版本号： v26.4.23.1
*唯一标识：8ec22772-ea97-4cc1-870c-612ff454cf00
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/28 14:19:50
*描述：Controller控制器类
*
*=====================================================================
*修改标记
*修改时间：2018/10/28 14:19:50
*修改人： yswenli
*版本号： v26.4.23.1
*描述：Controller控制器类
*
*****************************************************************************/
using SAEA.Common.IO;
using SAEA.Common.Serialization;
using System.IO;
using System.Text;

namespace SAEA.MVC
{
    /// <summary>
    /// MVC控制器
    /// </summary>
    public abstract class Controller
    {
        /// <summary>
        /// UserHostAddress
        /// </summary>
        public string UserHostAddress
        {
            get
            {
                return HttpContext.Current.Request.UserHostAddress;
            }
        }
        /// <summary>
        /// 返回数据流
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        protected DataResult Data(Stream stream)
        {
            return new DataResult(stream);
        }
        /// <summary>
        /// 返回数据流
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        protected DataResult Data(byte[] data)
        {
            return new DataResult(data);
        }

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
        protected ActionResult File(string filePath)
        {
            if (FileHelper.Exists(filePath))

                return new FileResult(filePath, HttpContext.Current.IsStaticsCached);
            else
                return new ContentResult($"o_o，找不到文件", System.Net.HttpStatusCode.NotFound);
        }

        /// <summary>
        /// 大数据
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        protected ActionResult BigData(string filePath)
        {
            if (FileHelper.Exists(filePath))

                return new BigDataResult(filePath);
            else
                return new ContentResult($"o_o，找不到文件", System.Net.HttpStatusCode.NotFound);
        }

        /// <summary>
        /// 服务器发送事件(Server-Sent Events)
        /// </summary>
        /// <returns></returns>
        protected EventStream GetEventStream()
        {
            return new EventStream();
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
            if (HttpContext.Current.Request.Headers.ContainsKey("X-Requested-With") || HttpContext.Current.Request.Headers.ContainsKey("x-requested-with"))
            {
                return true;
            }
            return false;
        }
    }
}