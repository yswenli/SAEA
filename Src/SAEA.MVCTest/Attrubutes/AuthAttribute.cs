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
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;

using JWT.Net;
using JWT.Net.Exceptions;

using SAEA.MVC;
using SAEA.MVCTest.Model;

namespace SAEA.MVCTest.Attrubutes
{
    public class AuthAttribute : ActionFilterAttribute
    {
        static readonly string _pwd = "yswenli";

        JWTPackage<UserInfo> _jwt = null;

        public AuthAttribute() : base()
        {

        }

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


        public override void OnActionExecuted(ref ActionResult result)
        {

        }
    }
}
