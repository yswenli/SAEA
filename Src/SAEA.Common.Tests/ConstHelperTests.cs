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
*命名空间：SAEA.Common.Tests
*文件名： ConstHelperTests
*版本号： net10.0
*唯一标识：3aebbb8c-3a06-4a83-925b-f00faf79091a
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 18:21:44
*描述：ConstHelper 跨域默认方法清单的单元测试
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 18:21:44
*修改人： yswenli
*版本号： net10.0
*描述：新建用例，校验跨域响应头方法清单含六谓词
*
*****************************************************************************/
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SAEA.Common.Tests
{
    /// <summary>
    /// 校验跨域响应头文本的默认允许方法清单
    /// </summary>
    [TestClass]
    public class ConstHelperTests
    {
        /// <summary>
        /// 验证两条方法清单同时含 PATCH、HEAD，且原有 GET/POST/PUT/DELETE/OPTIONS 未被删除
        /// </summary>
        [TestMethod]
        public void CrossDomain_DefaultMethods_ContainsPatchAndHeadAndKeepsLegacyVerbs()
        {
            //方法清单是拼装时的固定字面量，与自定义 header 缓存状态无关，因此无需先重置缓存
            var crossDomain = ConstHelper.CrossDomain;
            var lines = crossDomain.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            var allowMethods = Array.Find(lines, item => item.StartsWith("Access-Control-Allow-Methods:", StringComparison.Ordinal));
            var requestMethods = Array.Find(lines, item => item.StartsWith("Access-Control-Request-Methods:", StringComparison.Ordinal));

            Assert.IsNotNull(allowMethods, "跨域响应头缺少 Access-Control-Allow-Methods 行");
            Assert.IsNotNull(requestMethods, "跨域响应头缺少 Access-Control-Request-Methods 行");

            foreach (var method in new[] { "GET", "HEAD", "POST", "PUT", "DELETE", "PATCH", "OPTIONS" })
            {
                StringAssert.Contains(allowMethods, method);
                StringAssert.Contains(requestMethods, method);
            }
        }
    }
}