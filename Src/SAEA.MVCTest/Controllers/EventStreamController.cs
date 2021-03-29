/****************************************************************************
*项目名称：SAEA.MVCTest.Controllers
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.MVCTest.Controllers
*类 名 称：EventStreamController
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/1/6 13:57:09
*描述：
*=====================================================================
*修改时间：2021/1/6 13:57:09
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Threading;

using SAEA.MVC;
using SAEA.MVCTest.Attrubutes;

namespace SAEA.MVCTest.Controllers
{
    /// <summary>
    /// EventStreamController
    /// </summary>
    public class EventStreamController : Controller
    {
        /// <summary>
        /// 发送通知
        /// </summary>
        /// <returns></returns>
        [Auth]
        public ActionResult SendNotice()
        {
            try
            {
                var es = GetEventStream();

                for (int i = 0; ; i++)
                {
                    var str = $"SAEA.MVC EventStream Test {i}";

                    es.ServerSent(str);

                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {

            }
            return Empty();
        }
    }
}
