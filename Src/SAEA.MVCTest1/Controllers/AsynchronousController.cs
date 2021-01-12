/****************************************************************************
*项目名称：SAEA.MVCTest.Controllers
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.MVCTest.Controllers
*类 名 称：AsynchronousController
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/6/18 10:28:33
*描述：
*=====================================================================
*修改时间：2020/6/18 10:28:33
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.MVC;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.MVCTest.Controllers
{
    /// <summary>
    /// 支持异步方法
    /// </summary>
    public class AsynchronousController : Controller
    {
        /// <summary>
        /// Hello
        /// </summary>
        /// <returns></returns>
        public async Task<ActionResult> Hello()
        {
            return await Task.Run(() =>
            {
                return Content("Hello");
            });
        }

        /// <summary>
        /// Test
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<ActionResult> Test(string id = "1")
        {
            return await Task.Run(() =>
            {
                return Content($"Test id:{id}");
            });
        }
    }
}
