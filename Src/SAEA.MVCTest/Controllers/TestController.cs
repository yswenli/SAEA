using SAEA.MVC;
using SAEA.MVCTest.Attrubutes;
using SAEA.MVCTest.Model;
using System.Threading;

namespace SAEA.MVCTest.Controllers
{
    /// <summary>
    /// test
    /// </summary>
    public class TestController: Controller
    {
        /// <summary>
        /// Get
        /// </summary>
        /// <returns></returns>

        public ActionResult Get()
        {
            return Content("this is a test!中文测试！Zh Test!");
        }

        [Auth]
        [HttpGet]
        [HttpPost]
        public ActionResult Other(UserInfo userInfo)
        {
            return Json(userInfo);
        }

        public ActionResult Timeout()
        {
            int s = 2 * 1000;
            System.Console.WriteLine($"[{System.DateTime.Now}] 开始执行Timeout方法，将睡眠{s}毫秒...");
            Thread.Sleep(s);
            System.Console.WriteLine($"[{System.DateTime.Now}] Timeout方法执行完成！");
            return Content($"已成功等待{s}毫秒！");
        }
        
        public ActionResult SleepSync(int milliseconds)
        {
            System.Console.WriteLine($"[{System.DateTime.Now}] 开始执行SleepSync方法，将睡眠{milliseconds}毫秒...");
            Thread.Sleep(milliseconds);
            System.Console.WriteLine($"[{System.DateTime.Now}] SleepSync方法执行完成！");
            return Content($"已成功同步等待{milliseconds}毫秒！");
        }
        
        public async System.Threading.Tasks.Task<ActionResult> SleepAsync(int milliseconds)
        {
            System.Console.WriteLine($"[{System.DateTime.Now}] 开始执行SleepAsync方法，将睡眠{milliseconds}毫秒...");
            await System.Threading.Tasks.Task.Delay(milliseconds);
            System.Console.WriteLine($"[{System.DateTime.Now}] SleepAsync方法执行完成！");
            return Content($"已成功异步等待{milliseconds}毫秒！");
        }

    }
}
