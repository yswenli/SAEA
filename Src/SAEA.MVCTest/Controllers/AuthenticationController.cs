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
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.MVC;
using SAEA.MVCTest.Model;
using System;
using System.Collections.Generic;
using System.Text;
using SAEA.MVCTest.Attrubutes;

namespace SAEA.MVCTest.Controllers
{
    public class AuthenticationController : Controller
    {
        [Log2Atrribute]
        public ActionResult GetList()
        {
            var list = new List<UserInfo>();

            list.Add(new UserInfo()
            {
                ID = 1,
                NickName = "111",
                UserName = "222"
            });

            var objs = new object[3];
            objs[0] = 1;
            objs[1] = list;
            objs[2] = "aaa";

            return Json(objs);
        }
    }
}
