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
*修改时间：2026/11/19 15:13:26
*修改人： yswenli
*版本号： net10.0
*描述：新增，提供 64 KB 二进制大数据入口与普通文本对照入口
*
*****************************************************************************/
using SAEA.MVC.Controllers;
using SAEA.MVC.Results;
using System.IO;

namespace SAEA.MVC.Tests.Controllers
{
    /// <summary>
    /// 大数据结果用例的控制器：一个返回 IBigDataResult 的入口与一个普通文本入口作对照。
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
    }
}
