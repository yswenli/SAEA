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
*文件名： HttpResponseAttachmentTests
*版本号： net10.0
*唯一标识：a7a66ff4-eddf-4442-9048-76ac34ad6184
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/26 17:46:21
*描述：HttpResponse.SetCached 写 Content-Disposition 的用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/26 17:46:21
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖带文件名与无文件名的文件结果
*
*****************************************************************************/
using System;
using System.IO;
using System.Net;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.Http.Model;

namespace SAEA.Http.Tests
{
    /// <summary>
    /// HttpResponse.SetCached 对文件结果写出 Content-Disposition 的用例。
    /// </summary>
    [TestClass]
    public class HttpResponseAttachmentTests
    {
        private const string ContentDispositionKey = "Content-Disposition";

        /// <summary>
        /// 带下载文件名的文件结果应写出 attachment 头，并带 UTF-8 扩展值。
        /// </summary>
        [TestMethod]
        public void SetCached_FileResultWithFileName_WritesContentDisposition()
        {
            var response = CreateResponse();

            response.SetCached(new HttpFileResult(new MemoryStream(new byte[] { 1, 2, 3 }), "application/octet-stream", HttpStatusCode.OK, "报告.pdf"));

            var value = response.Headers[ContentDispositionKey];

            StringAssert.StartsWith(value, "attachment;");
            StringAssert.Contains(value, "filename*=UTF-8''%E6%8A%A5%E5%91%8A.pdf");
        }

        /// <summary>
        /// 未指定下载文件名时不应写 Content-Disposition。
        /// </summary>
        [TestMethod]
        public void SetCached_FileResultWithoutFileName_DoesNotWriteHeader()
        {
            var response = CreateResponse();

            response.SetCached(new HttpFileResult(new MemoryStream(new byte[] { 1, 2, 3 }), "application/octet-stream"));

            Assert.IsFalse(response.Headers.ContainsKey(ContentDispositionKey), "文件名为空时不应写 Content-Disposition");
        }

        /// <summary>
        /// 响应没有公开构造函数，测试用非公开构造创建实例。
        /// </summary>
        /// <returns>可用的空响应</returns>
        private static HttpResponse CreateResponse()
        {
            return (HttpResponse)Activator.CreateInstance(typeof(HttpResponse), true);
        }
    }
}