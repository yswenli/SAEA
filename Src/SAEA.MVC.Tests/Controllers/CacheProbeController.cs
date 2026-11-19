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
*文件名： CacheProbeController
*版本号： net10.0
*唯一标识：4c1a7f26-8b3d-4e59-a2c4-6f0d9b81e537
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/17 22:00:00
*描述：输出缓存 query 键用例的控制器
*
*=====================================================================
*修改标记
*修改时间：2026/11/17 22:00:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，提供带真实执行计数的输出缓存入口以验证 query 参与缓存键
*
*****************************************************************************/
using System.Threading;

namespace SAEA.MVC.Tests.Controllers
{
    /// <summary>
    /// 输出缓存 query 键的控制器：回显路径参数与 query 里的 tag，并附上真实执行次数。
    /// </summary>
    public class CacheProbeController : Controller
    {
        private static int _executions;

        /// <summary>
        /// 带输出缓存的路径参数入口。
        /// </summary>
        /// <param name="id">路径中的 {id} 段</param>
        /// <returns>形如 id|tag|执行次数的文本</returns>
        [HttpGetAttribute("cacheprobe/{id}")]
        [OutputCache(60)]
        public string Probe(string id)
        {
            var tag = string.Empty;

            var query = HttpContext.Current.Request.Query;

            if (query != null && query.TryGetValue("tag", out var value))
                tag = value ?? string.Empty;

            // 计数器统计真实执行次数，命中缓存时不会自增
            var executions = Interlocked.Increment(ref _executions);

            return id + "|" + tag + "|" + executions;
        }
    }
}