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
*文件名： BigDataProbeController
*版本号： net10.0
*唯一标识：965baa60-62c7-4a22-9dd1-1dfcd5927157
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 15:13:26
*描述：大数据结果收尾用例的控制器
*
*=====================================================================
*修改标记
*修改时间：2026/11/26 17:59:02
*修改人： yswenli
*版本号： net10.0
*描述：新增慢速大数据入口，供客户端中断停止读取用例使用
*
*****************************************************************************/
using SAEA.MVC.Controllers;
using SAEA.MVC.Results;
using System.IO;

namespace SAEA.MVC.Tests.Controllers
{
    /// <summary>
    /// 大数据结果用例的控制器：提供二进制大数据、慢速流与普通文本三个入口。
    /// </summary>
    public class BigDataProbeController : Controller
    {
        /// <summary>
        /// 返回 64 KB 二进制大数据结果。
        /// </summary>
        /// <returns>大数据结果</returns>
        public ActionResult Payload()
        {
            var payload = new byte[64 * 1024];

            for (var i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)(i % 251);
            }

            return new BigDataResult(new MemoryStream(payload), "application/octet-stream");
        }

        /// <summary>
        /// 普通文本入口，用于对照普通响应仍会走缓存收尾。
        /// </summary>
        /// <returns>固定文本</returns>
        public string Plain()
        {
            return "bigdata-plain";
        }

        /// <summary>
        /// 慢速大数据入口：按 16 KB 分块读取并逐块延时，供客户端中断用例观察读循环是否停止。
        /// </summary>
        /// <returns>大数据结果</returns>
        public ActionResult Slow()
        {
            return new BigDataResult(new CountingSlowStream(), "application/octet-stream");
        }
    }
}
