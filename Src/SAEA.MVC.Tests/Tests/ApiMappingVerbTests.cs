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
*命名空间：SAEA.MVC.Tests
*文件名： ApiMappingVerbTests
*版本号： net10.0
*唯一标识：60bc9558-b0a2-4084-9b3d-7a8bc57df57c
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 16:20:00
*描述：ApiMapping 谓词识别用例，验证七个谓词特性都按运行期同一口径返回
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 16:20:00
*修改人： yswenli
*版本号： net10.0
*描述：新增
*
*****************************************************************************/
using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.Tests.Controllers;
using SAEA.MVC.Tool.CodeGenerte;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// ApiMapping 谓词识别用例：SDK 生成与运行期 405 判定必须共用同一套谓词清单。
    /// </summary>
    [TestClass]
    public class ApiMappingVerbTests
    {
        /// <summary>
        /// 只声明 PUT 的两段式动作只返回 PUT，避免再生成必然 405 的 GET/POST 条目。
        /// </summary>
        [TestMethod]
        public void ApiMapping_GetDeclaredVerbs_PutOnly_ReturnsPutOnly()
        {
            var verbs = ApiMapping.GetDeclaredVerbs(typeof(RestVerbController).GetMethod("PutOnly"));

            CollectionAssert.AreEqual(new[] { "PUT" }, verbs, "只声明 [HttpPut] 时应只返回 PUT");
        }

        /// <summary>
        /// 只带 [Route]、不带任何谓词特性的动作返回 null，表示不限谓词。
        /// </summary>
        [TestMethod]
        public void ApiMapping_GetDeclaredVerbs_NoVerbAttribute_ReturnsNull()
        {
            var verbs = ApiMapping.GetDeclaredVerbs(typeof(RestVerbController).GetMethod("RouteOnly"));

            Assert.IsNull(verbs, "未声明谓词特性时应返回 null 表示不限谓词");
        }

        /// <summary>
        /// 多个谓词特性按 HttpMethodCatalog.All 的顺序去重返回。
        /// </summary>
        [TestMethod]
        public void ApiMapping_GetDeclaredVerbs_MultiVerb_ReturnsCatalogOrder()
        {
            var verbs = ApiMapping.GetDeclaredVerbs(typeof(RestVerbController).GetMethod("BothVerbs"));

            CollectionAssert.AreEqual(new[] { "GET", "POST" }, verbs, "多谓词应按目录顺序返回 GET、POST");
        }
    }
}