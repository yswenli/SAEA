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
 *文件名： HttpContentResultTests
 *版本号： net10.0
 *唯一标识：6e88605f-b232-4f7d-8cf2-01278d3d69c0
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/17 18:40:00
 *描述：HttpContentResult 默认内容类型用例
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/17 18:40:00
 *修改人： yswenli
 *版本号： net10.0
 *描述：HttpContentResult 默认内容类型用例
 *
 *****************************************************************************/
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.Http.Model;

namespace SAEA.Http.Tests
{
    /// <summary>
    /// 文本结果默认内容类型的用例。
    /// </summary>
    [TestClass]
    public class HttpContentResultTests
    {
        /// <summary>
        /// 只给编码的构造函数默认内容类型应为 text/plain。
        /// </summary>
        [TestMethod]
        public void HttpContentResult_DefaultContentType_IsTextPlain()
        {
            var result = new HttpContentResult("x", Encoding.UTF8);

            Assert.AreEqual("text/plain; charset=utf-8", result.ContentType, "默认内容类型应为 text/plain; charset=utf-8");
        }

        /// <summary>
        /// 只给文本的构造函数同样落到 text/plain，不能回到 text/plane。
        /// </summary>
        [TestMethod]
        public void HttpContentResult_TextOnly_ContentTypeIsTextPlain()
        {
            var result = new HttpContentResult("x");

            Assert.AreEqual("text/plain; charset=utf-8", result.ContentType, "默认内容类型应为 text/plain; charset=utf-8");
        }
    }
}