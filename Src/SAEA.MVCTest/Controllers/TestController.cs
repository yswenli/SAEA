using SAEA.MVC;
using SAEA.MVCTest.Attrubutes;
using SAEA.MVCTest.Model;

using System.Threading;
using System.Threading.Tasks;

namespace SAEA.MVCTest.Controllers
{
    /// <summary>
    /// 基础功能测试控制器，覆盖文本输出、鉴权、超时与同步/异步等待
    /// </summary>
    public class TestController: Controller
    {
        /// <summary>
        /// 返回中英文混合文本，用于验证中文编码输出
        /// </summary>
        /// <returns>固定文本内容</returns>

        public ActionResult Get()
        {
            return Content("this is a test!中文测试！Zh Test!");
        }

        /// <summary>
        /// 同时支持 GET 与 POST，校验 JWT 后原样返回用户信息
        /// </summary>
        /// <param name="userInfo">按属性名绑定的用户信息</param>
        /// <returns>用户信息的 JSON 结果</returns>
        [Auth]
        [HttpGet]
        [HttpPost]
        public ActionResult Other(UserInfo userInfo)
        {
            return Json(userInfo);
        }

        /// <summary>
        /// 同步睡眠 2000 毫秒，用于验证请求超时处理
        /// </summary>
        /// <returns>说明已等待毫秒数的文本</returns>
        public ActionResult Timeout()
        {
            int s = 2 * 1000;
            System.Console.WriteLine($"[{System.DateTime.Now}] 开始执行Timeout方法，将睡眠{s}毫秒...");
            Thread.Sleep(s);
            System.Console.WriteLine($"[{System.DateTime.Now}] Timeout方法执行完成！");
            return Content($"已成功等待{s}毫秒！");
        }
        
        /// <summary>
        /// 用 Thread.Sleep 同步阻塞指定毫秒数，用于对比同步与异步等待的并发表现
        /// </summary>
        /// <param name="milliseconds">阻塞毫秒数</param>
        /// <returns>说明已同步等待毫秒数的文本</returns>
        public ActionResult SleepSync(int milliseconds)
        {
            System.Console.WriteLine($"[{System.DateTime.Now}] 开始执行SleepSync方法，将睡眠{milliseconds}毫秒...");
            Thread.Sleep(milliseconds);
            System.Console.WriteLine($"[{System.DateTime.Now}] SleepSync方法执行完成！");
            return Content($"已成功同步等待{milliseconds}毫秒！");
        }
        
        /// <summary>
        /// 用 Task.Delay 异步等待指定毫秒数，不阻塞线程
        /// </summary>
        /// <param name="milliseconds">等待毫秒数</param>
        /// <returns>说明已异步等待毫秒数的文本</returns>
        public async Task<ActionResult> SleepAsync(int milliseconds)
        {
            System.Console.WriteLine($"[{System.DateTime.Now}] 开始执行SleepAsync方法，将睡眠{milliseconds}毫秒...");
            await Task.Delay(milliseconds);
            System.Console.WriteLine($"[{System.DateTime.Now}] SleepAsync方法执行完成！");
            return Content($"已成功异步等待{milliseconds}毫秒！");
        }

    }
}
