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
*文件名： FastPathController
*版本号： net10.0
*唯一标识：6b3a83e0-d7b8-4c0a-b0dc-1ecff2ec94dd
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/18 13:45:00
*描述：未配置 ActionTimeout 时直调快路径的探针控制器
*
*=====================================================================
*修改标记
*修改时间：2026/11/18 13:45:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，为同步/异步直调快路径与异常文案用例提供入口
*
*****************************************************************************/
using System;
using System.Threading.Tasks;

namespace SAEA.MVC.Tests.Controllers
{
    /// <summary>
    /// 直调快路径的探针控制器：同步成功、同步抛异常与异步成功三个入口。
    /// </summary>
    public class FastPathController : Controller
    {
        /// <summary>
        /// 同步成功入口：未配置 ActionTimeout 时应直接调用并返回 200。
        /// </summary>
        /// <returns>固定文本</returns>
        [HttpGet]
        public string SyncOk()
        {
            return "fastpath-sync-ok";
        }

        /// <summary>
        /// 同步抛异常入口：异常应被框架转成 500，文案沿用旧路径的 AggregateException 形态。
        /// </summary>
        /// <returns>不会返回</returns>
        [HttpGet]
        public string SyncThrows()
        {
            throw new InvalidOperationException("fastpath-boom");
        }

        /// <summary>
        /// 异步成功入口：直调快路径要能等到 Task 完成并取出 Task&lt;string&gt; 的结果。
        /// </summary>
        /// <returns>完成后的固定文本</returns>
        [HttpGet]
        public async Task<string> AsyncOk()
        {
            await Task.Yield();

            return "fastpath-async-ok";
        }
    }
}