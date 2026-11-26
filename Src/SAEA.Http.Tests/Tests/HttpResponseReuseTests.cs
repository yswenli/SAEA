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
*命名空间：SAEA.Http.Tests
*文件名： HttpResponseReuseTests
*版本号： net10.0
*唯一标识：56093c5b-f593-4296-96bb-9935416ef2f8
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/26 20:40:52
*描述：响应实例复位给下一条请求时请求代际号递增的用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/26 20:40:52
*修改人： yswenli
*版本号： net10.0
*描述：新增，验证复位给下一条请求时请求代际号递增
*
*****************************************************************************/
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SAEA.Http.Tests
{
    /// <summary>
    /// 响应实例复用用例：复位给下一条请求时请求代际号必须递增，长连接据此停止写入旧响应。
    /// </summary>
    [TestClass]
    public class HttpResponseReuseTests
    {
        /// <summary>
        /// 连续复位两次时代际号逐次加一，长连接据此区分新旧请求。
        /// </summary>
        [TestMethod]
        public void ResetForNextRequest_CalledTwice_IncrementsRequestGeneration()
        {
            var response = new HttpResponse();

            var initial = response.RequestGeneration;

            response.ResetForNextRequest();

            var afterFirst = response.RequestGeneration;

            response.ResetForNextRequest();

            var afterSecond = response.RequestGeneration;

            Assert.AreEqual(initial + 1, afterFirst, "首次复位后代际号应加一，初始 " + initial + "，实际 " + afterFirst);
            Assert.AreEqual(initial + 2, afterSecond, "再次复位后代际号应继续递增，实际 " + afterSecond);
        }
    }
}