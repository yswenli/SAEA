/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
 *Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.MVC.Tests.Controllers
*文件名： AsyncActionController
*版本号： net10.0
*唯一标识：2d9c8f4e-5b16-4c7a-9e35-7f0a1c6d8b24
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/18 16:30:00
*描述：真正异步 action 的探针控制器
*
*=====================================================================
*修改标记
*修改时间：2026/11/18 16:30:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，为异步成功、异步抛异常、超时 408 与异步 JSON 结果用例提供入口
*
*****************************************************************************/
using System;
using System.Threading.Tasks;

namespace SAEA.MVC.Tests.Controllers
{
    /// <summary>
    /// 真正异步 action 的探针控制器：异步成功、异步抛异常、慢动作与 JSON 结果四个入口，
    /// 另带一个把宿主动作超时改小的入口，供 408 用例在独立宿主上启用超时包装。
    /// </summary>
    public class AsyncActionController : Controller
    {
        /// <summary>
        /// 异步成功入口：让出一次线程后返回固定文本，验证 200 与正文。
        /// </summary>
        /// <returns>完成后的固定文本</returns>
        [HttpGetAttribute]
        public async Task<string> Ok()
        {
            await Task.Yield();

            return "asyncaction-ok";
        }

        /// <summary>
        /// 异步抛异常入口：异常在异步状态机内抛出，框架应按 AggregateException 形态转成 500。
        /// </summary>
        /// <returns>不会返回</returns>
        [HttpGetAttribute]
        public async Task<string> Throws()
        {
            await Task.Yield();

            throw new InvalidOperationException("asyncaction-boom");
        }

        /// <summary>
        /// 异步慢动作入口：不观察取消令牌，配置 ActionTimeout 后由框架自己返回 408。
        /// </summary>
        /// <returns>未超时时返回固定文本</returns>
        [HttpGetAttribute]
        public async Task<string> Slow()
        {
            await Task.Delay(TimeSpan.FromSeconds(30));

            return "asyncaction-slow-done";
        }

        /// <summary>
        /// 异步 JSON 入口：让出一次线程后返回 JsonResult，验证正文与内容类型。
        /// </summary>
        /// <returns>固定结构的 JSON 结果</returns>
        [HttpGetAttribute]
        public async Task<JsonResult> JsonData()
        {
            await Task.Yield();

            return Json(new { name = "saea", value = 42 });
        }

        /// <summary>
        /// 把本宿主的动作超时改成 1 秒；动作超时与连接空闲回收是两项独立配置。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpGetAttribute]
        public string ArmShortTimeout()
        {
            HttpContext.Current.WebConfig.ActionTimeout = 1;

            return "asyncaction-armed";
        }
    }
}