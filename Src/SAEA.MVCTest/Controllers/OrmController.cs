/****************************************************************************
*项目名称：SAEA.MVCTest.Controllers
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.MVCTest.Controllers
*类 名 称：OrmController
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/3/12 13:52:34
*描述：
*=====================================================================
*修改时间：2021/3/12 13:52:34
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using SAEA.MVC;
using SAEA.MVCTest.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.MVCTest.Controllers
{
    public class OrmController:Controller
    {
        /// <summary>
        /// 新增服务器配置
        /// </summary>
        /// <param name="serverConfigLog">服务器配置</param>
        /// <returns></returns>
        public ActionResult ServerConfigLogAdd(ServerConfigLog serverConfigLog)
        {
            return Json(serverConfigLog);
        }

        /// <summary>
        /// 更新服务器配置
        /// </summary>
        /// <param name="serverConfigLog">服务器配置</param>
        /// <returns></returns>
        public ActionResult ServerConfigLogUpdate(ServerConfigLog serverConfigLog)
        {
            return Json(serverConfigLog);
        }
    }
}
