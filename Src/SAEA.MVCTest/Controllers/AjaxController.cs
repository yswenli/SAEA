using SAEA.MVC;
using SAEA.MVCTest.Model;

namespace SAEA.MVCTest.Controllers
{
    /// <summary>
    /// Ajax 请求识别示例控制器，按请求是否带 Ajax 标识返回不同文本
    /// </summary>
    public class AjaxController : Controller
    {
        /// <summary>
        /// 回显 str 参数，并按是否 Ajax 请求给出不同提示
        /// </summary>
        /// <param name="str">待回显的文本</param>
        /// <returns>包含原始文本与请求类型判断的文本结果</returns>
        public ActionResult Test(string str)
        {
            if (IsAjaxRequest())
            {
                return Content($"str={str}. \r\nthis is an ajax request!");
            }
            else
            {
                return Content($"str={str}. \r\nthis is not an ajax request!");
            }
        }


        /// <summary>
        /// 与 Test 行为一致的回显动作，用于对比框架对同名逻辑的路由处理
        /// </summary>
        /// <param name="str">待回显的文本</param>
        /// <returns>包含原始文本与请求类型判断的文本结果</returns>
        public ActionResult Test2(string str)
        {
            if (IsAjaxRequest())
            {
                return Content($"str={str}. \r\nthis is an ajax request!");
            }
            else
            {
                return Content($"str={str}. \r\nthis is not an ajax request!");
            }
        }


        /// <summary>
        /// 把传入的用户信息序列化为 JSON 返回
        /// </summary>
        /// <param name="userInfo">用户信息，由请求参数按属性名绑定</param>
        /// <returns>用户信息的 JSON 结果</returns>
        public ActionResult Test3(UserInfo userInfo)
        {
            return Json(userInfo);
        }

        /// <summary>
        /// 返回由基本类型参数拼接而成的 JSON 文本
        /// </summary>
        /// <param name="id">标识</param>
        /// <param name="userName">登录名</param>
        /// <param name="nickName">昵称</param>
        /// <returns>拼接后的 JSON 文本结果</returns>
        public ActionResult Test4(int id,string userName,string nickName)
        {
            return Json($"id:{id},userName:{userName},nickName:{nickName}");
        }
    }
}
