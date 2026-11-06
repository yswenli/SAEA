/****************************************************************************
*项目名称：SAEA.MVCTest.Attrubutes
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.MVCTest.Attrubutes
*类 名 称：AuthAttribute
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/1/7 18:21:46
*描述：
*=====================================================================
*修改时间：2020/1/7 18:21:46
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using System;

using JWT.Net;
using JWT.Net.Exceptions;

using SAEA.MVC;
using SAEA.MVCTest.Model;

namespace SAEA.MVCTest.Attrubutes
{
    /// <summary>
    /// JWT 鉴权过滤器，从 Authorization 请求头解析用户令牌并写入会话
    /// </summary>
    public class AuthAttribute : ActionFilterAttribute
    {
        static readonly string _pwd = "yswenli";

        JWTPackage<UserInfo> _jwt = null;

        /// <summary>
        /// 初始化鉴权过滤器
        /// </summary>
        public AuthAttribute() : base()
        {

        }

        /// <summary>
        /// 动作执行前解析 Authorization 头中的 JWT，成功时把用户信息写入会话
        /// </summary>
        /// <returns>校验通过返回空结果放行，否则返回说明失败原因的文本结果</returns>
        public override ActionResult OnActionExecuting()
        {
            var result = string.Empty;
            try
            {
                if (HttpContext.Current.Request.Headers.ContainsKey("Authorization"))
                {
                    var val = HttpContext.Current.Request.Headers["Authorization"].ToString();

                    val = val.Replace(JWTPackage.Prex, "");

                    _jwt = JWTPackage<UserInfo>.Parse(val, _pwd);

                    HttpContext.Current.Session["userInfo"] = _jwt;

                    result = "OK";
                }
            }
            catch (IllegalTokenException iex)
            {
                result = $"解析失败：{iex.Message}";
            }
            catch (TokenExpiredException tex)
            {
                result = $"解析失败：{tex.Message}";
            }
            catch (SignatureVerificationException sex)
            {
                result = $"解析失败：{sex.Message}";
            }
            catch (Exception ex)
            {
                result = $"解析失败：{ex.Message}";
            }
            if (result == "OK")
            {
                return EmptyResult.Default;
            }
            else
            {
                result = $"解析失败,缺少jwt token";
            }
            return ContentResult.Get(result);
        }


        /// <summary>
        /// 动作执行后的回调，当前不做任何处理
        /// </summary>
        /// <param name="result">动作执行结果，可在回调中替换</param>
        public override void OnActionExecuted(ref ActionResult result)
        {

        }
    }
}
