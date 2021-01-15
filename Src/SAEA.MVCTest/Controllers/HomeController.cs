/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MVCTest.Controllers
*文件名： HomeController
*版本号： v6.0.0.1
*唯一标识：e00bb57f-e3ee-4efe-a7cf-f23db767c1d0
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/10 16:43:26
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/10 16:43:26
*修改人： yswenli
*版本号： v6.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.MVC;
using SAEA.MVCTest.Attrubutes;
using SAEA.MVCTest.Model;

namespace SAEA.MVCTest.Controllers
{
    /// <summary>
    /// 测试实例代码
    /// </summary>
    [LogAtrribute]
    public class HomeController : Controller
    {
        /// <summary>
        /// 日志拦截
        /// 内容输出
        /// </summary>
        /// <returns></returns>              
        [Log2Atrribute]
        [OutputCache]
        public ActionResult Index()
        {
            HttpContext.Current.Session["userID"] = "yswenli";

            return Content("Hello,I'm SAEA.MVC！");
        }


        [Log2Atrribute]
        [OutputCache]
        public ActionResult Index2()
        {
            return Content($"Hello,I'm SAEA.MVC2,develop by {HttpContext.Current.Session["userID"]}");
        }

        [Log2Atrribute]
        [OutputCache]
        public ActionResult Index3()
        {
            var ip = HttpContext.Current.Request.UserHostAddress;

            var rip = HttpContext.Current.Request.GetRealIp();

            var browser = HttpContext.Current.Request.Browser;

            return Content($"Hello,I'm SAEA.MVC3！ip:{ip} rip:{rip} browser:{browser.BrowserName} os:{browser.OSName}");
        }
        /// <summary>
        /// 支持基本类型参数
        /// json序列化
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>

        [HttpGet]
        [HttpPost]
        public ActionResult Get(int id)
        {
            return Json(new { Name = "yswenli", Sex = "男" });
        }
        /// <summary>
        /// 底层对象调用
        /// </summary>
        /// <returns></returns>
        public ActionResult Show()
        {
            var response = HttpContext.Current.Response;

            response.ContentType = "text/html; charset=utf-8";

            response.Write("<h3>测试一下那个response对象使用情况！</h3>参考消息网4月12日报道外媒称，法国一架“幻影-2000”战机意外地对本国一家工厂投下了炸弹。据俄罗斯卫星网4月12日援引法国蓝色电视台报道，事故于当地时间10日发生在卢瓦尔省，当时两架法国空军的飞机飞过韦尔尼松河畔诺让市镇上空，一枚炸弹从其中一架飞机上掉了下来，直接掉在了佛吉亚公司的工厂里。与此同时，有两人受伤。一名目击者称，“起初是两架战机飞过，然后我们都听到了物体撞击的声音，声音相当响，甚至盖过了飞过的飞机的噪音。”法国空军代表称，掉在工厂里的炸弹是演习用的，里面没有装炸药，本来是要将它投到离兰斯市不远的靶场。这名代表称事件“非常非常罕见”，目前正进行调查。");

            return Empty();
        }

        /// <summary>
        /// 单id参数
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        public ActionResult Update(int id)
        {
            return Content($"HttpGet Update id:{id}");
        }
        /// <summary>
        /// 基本类型参数、实体混合填充
        /// </summary>
        /// <param name="isFemale"></param>
        /// <param name="userInfo"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult Update(bool isFemale, UserInfo userInfo = null)
        {
            return Json(userInfo);
        }

        /// <summary>
        /// test model
        /// </summary>
        /// <param name="isFemale"></param>
        /// <param name="userInfo"></param>
        /// <returns></returns>
        public ActionResult Set(bool isFemale, UserInfo userInfo)
        {
            return Json(userInfo);
        }

        /// <summary>
        /// Get 多参数测试
        /// </summary>
        /// <param name="version"></param>
        /// <param name="basePamars"></param>
        /// <param name="pagedPamars"></param>
        /// <returns></returns>
        public ActionResult GetModels(string version, BasePamars basePamars, PagedPamars pagedPamars)
        {
            return Content($"version:{version}  basePamars:{Serialize(basePamars)}  pagedPamars:{Serialize(pagedPamars)}");
        }

        /// <summary>
        /// Test
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public ActionResult Test()
        {
            return Content("httppost test");
        }
    }
}
