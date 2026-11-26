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
*文件名： RangeProbeController
*版本号： net10.0
*唯一标识：623bb736-729e-429e-a538-4c105c96bbfd
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/26 10:10:00
*描述：控制器返回内存文件结果供 Range 用例使用
*
*=====================================================================
*修改标记
*修改时间：2026/11/26 10:10:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，提供 64 字节的内存 FileResult 入口
*
*****************************************************************************/
using System.IO;

using SAEA.MVC.Controllers;
using SAEA.MVC.Results;

namespace SAEA.MVC.Tests.Controllers
{
    /// <summary>
    /// Range 用例的控制器：返回内存中的 64 字节文件结果。
    /// </summary>
    public class RangeProbeController : Controller
    {
        /// <summary>
        /// 返回 64 字节文件结果，第 i 个字节的值为 i。
        /// </summary>
        /// <returns>文件结果</returns>
        public ActionResult FilePayload()
        {
            var payload = new byte[64];

            for (var i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)i;
            }

            return new FileResult(new MemoryStream(payload), "application/octet-stream");
        }
    }
}