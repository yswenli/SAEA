/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MVCTest.Common
*文件名： LogAtrribute
*版本号： v6.0.0.1
*唯一标识：2a261731-b8f6-47de-b2e4-aecf2e0e0c0f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/11 13:46:42
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/11 13:46:42
*修改人： yswenli
*版本号： v6.0.0.1
*描述：
*
*****************************************************************************/
using System.Diagnostics;
using System.Linq;
using System.Text;

using SAEA.Common;
using SAEA.Common.Serialization;
using SAEA.MVC;

using HttpContext = SAEA.MVC.HttpContext;

namespace SAEA.MVCTest.Attrubutes
{
    /// <summary>
    /// 自定义日志记录
    /// </summary>
    public sealed class LogAtrribute : ActionFilterAttribute
    {
        Stopwatch _stopwatch;

        /// <summary>
        /// 执行前
        /// </summary>
        /// <returns>返回值true为继续，false为终止</returns>
        public override bool OnActionExecuting()
        {
            _stopwatch = Stopwatch.StartNew();
            return true;
        }

        /// <summary>
        /// 执行后
        /// </summary>
        /// <param name="result"></param>
        public override void OnActionExecuted(ref ActionResult result)
        {
            _stopwatch.Stop();

            var inputStr = "";

            if (HttpContext.Current.Request.Parmas != null)
            {
                inputStr = SerializeHelper.Serialize(HttpContext.Current.Request.Parmas);
            }

            var outStr = "";
            if (result.Content != null && result.Content.Any())
            {
                outStr = Encoding.UTF8.GetString(result.Content);
            }
            ConsoleHelper.WriteLine($"LogAtrribute请求地址：{HttpContext.Current.Request.RelativeUrl},请求参数：{ inputStr},用时：{_stopwatch.ElapsedMilliseconds}ms,回复内容：{outStr}");
        }
    }
    /// <summary>
    /// 自定义日志记录
    /// </summary>
    public class Log2Atrribute : ActionFilterAttribute
    {
        /// <summary>
        /// 执行前
        /// </summary>
        /// <returns>返回值true为继续，false为终止</returns>
        public override bool OnActionExecuting()
        {
            return true;
        }

        /// <summary>
        /// 执行后
        /// </summary>
        /// <param name="result"></param>
        public override void OnActionExecuted(ref ActionResult result)
        {
            var inputStr = "";

            if (HttpContext.Current.Request.Parmas != null)
            {
                inputStr = SerializeHelper.Serialize(HttpContext.Current.Request.Parmas);
            }

            var outStr = "";
            if (result.Content != null && result.Content.Any())
            {
                outStr = Encoding.UTF8.GetString(result.Content);
            }
            ConsoleHelper.WriteLine($"Log2Atrribute请求地址：{HttpContext.Current.Request.RelativeUrl},请求参数：{inputStr},回复内容：{outStr}");
            LogHelper.Info("Log2Atrribute.OnActionExecuted", result);
        }
    }
}
