/****************************************************************************
*项目名称：SAEA.MVCTest.Controllers
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.MVCTest.Controllers
*类 名 称：AuthenticationController
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/8/27 16:43:20
*描述：
*=====================================================================
*修改时间：2020/8/27 16:43:20
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using System;

using JWT.Net;

using SAEA.MVC;
using SAEA.MVCTest.Attrubutes;
using SAEA.MVCTest.Model;

namespace SAEA.MVCTest.Controllers
{
    [LogAtrribute]
    public class AuthenticationController : Controller
    {

        static readonly string _pwd = "yswenli";

        public ActionResult Login(string userName, string pwd)
        {
            Console.WriteLine($"userName:{userName},pwd:{pwd}");

            var jwtp = new JWTPackage<UserInfo>(new UserInfo()
            {
                ID = 39654,
                UserName = userName,
                NickName = "yswenli"
            }, 180, _pwd);
            var keyValuePair = jwtp.GetAuthorizationBearer();
            HttpContext.Current.Response.Headers[keyValuePair.Item1] = keyValuePair.Item2;
            HttpContext.Current.Response.Write("Success");
            HttpContext.Current.Response.End();
            return Empty();
        }

    }
}
