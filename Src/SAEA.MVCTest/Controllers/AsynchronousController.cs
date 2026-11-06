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
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using SAEA.MVC;

using System.Threading.Tasks;

namespace SAEA.MVCTest.Controllers
{
    /// <summary>
    /// 支持异步方法
    /// </summary>
    public class AsynchronousController : Controller
    {
        /// <summary>
        /// 异步返回文本 Hello，用于验证等待型 action
        /// </summary>
        /// <returns>文本结果</returns>
        public async Task<ActionResult> Hello()
        {
            return await Task.Run(() =>
            {
                return Content("Hello");
            });
        }

        /// <summary>
        /// 异步返回带 id 的测试文本
        /// </summary>
        /// <param name="id">测试标识，默认 1</param>
        /// <returns>文本结果</returns>
        public async Task<ActionResult> Test(string id = "1")
        {
            return await Task.Run(() =>
            {
                return Content($"Test id:{id}");
            });
        }
    }
}
