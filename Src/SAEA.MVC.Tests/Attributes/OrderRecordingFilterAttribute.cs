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
*命名空间：SAEA.MVC.Tests.Attributes
*文件名： OrderRecordingFilterAttribute
*版本号： net10.0
*唯一标识：b12f98f9-6ef6-400a-bce0-cc268525730e
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 18:05:27
*描述：记录执行顺序的动作过滤器
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 18:05:27
*修改人： yswenli
*版本号： net10.0
*描述：新增，与 action 共用静态记录表以断言过滤器先于 action 执行
*
*****************************************************************************/
using System.Collections.Generic;

namespace SAEA.MVC.Tests.Attributes
{
    /// <summary>
    /// 把执行标记写入静态记录表，并与控制器 action 共用同一份表以断言调用顺序。
    /// </summary>
    public sealed class OrderRecordingFilterAttribute : ActionFilterAttribute
    {
        /// <summary>
        /// 执行顺序记录表，过滤器写 filter、action 写 action。
        /// </summary>
        public static readonly List<string> Records = new List<string>();

        /// <summary>
        /// 记录过滤器已执行并放行 action。
        /// </summary>
        /// <returns>null 表示继续执行 action</returns>
        public override ActionResult OnActionExecuting()
        {
            Records.Add("filter");

            return null;
        }

        /// <summary>
        /// action 已执行，无需额外处理。
        /// </summary>
        /// <param name="result">action 返回的结果</param>
        public override void OnActionExecuted(ref ActionResult result)
        {
        }
    }
}