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
*文件名： HttpResponseRangeTests
*版本号： net10.0
*唯一标识：b4bd56bc-2add-4a42-aa1d-7867ca8251f0
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/26 10:05:00
*描述：内存正文应用单区间 Range 的用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/26 10:05:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，覆盖 206 切片、416 空正文、非法与多区间回退 200、304 与 gzip 不启用
*
*****************************************************************************/
using System;
using System.Net;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.Http;

namespace SAEA.Http.Tests
{
    /// <summary>
    /// 内存正文应用单区间 Range 的用例：可满足改 206，不可满足改 416，其余保持 200 全量。
    /// </summary>
    [TestClass]
    public class HttpResponseRangeTests
    {
        private const string ContentRangeKey = "Content-Range";

        private const string AcceptRangesKey = "Accept-Ranges";

        /// <summary>
        /// 没有 Range 头时仍回 200 全量，但要带上 Accept-Ranges。
        /// </summary>
        [TestMethod]
        public void ApplyRangeHeader_NoRange_KeepsFullBody()
        {
            var response = CreateResponse(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

            response.ApplyRangeHeader(null);

            Assert.AreEqual(HttpStatusCode.OK, response.Status);
            Assert.AreEqual("bytes", Read(response, AcceptRangesKey));
            Assert.AreEqual(10, response.Body.Length);
            Assert.IsFalse(response.Headers.ContainsKey(ContentRangeKey));
        }

        /// <summary>
        /// 明确首末字节的区间应切成对应片段并回 206。
        /// </summary>
        [TestMethod]
        public void ApplyRangeHeader_FirstRange_ReturnsPartialContent()
        {
            var response = CreateResponse(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

            response.ApplyRangeHeader("bytes=0-3");

            Assert.AreEqual(HttpStatusCode.PartialContent, response.Status);
            CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 3 }, response.Body);
            Assert.AreEqual("bytes 0-3/10", Read(response, ContentRangeKey));
            Assert.AreEqual("bytes", Read(response, AcceptRangesKey));
        }

        /// <summary>
        /// 后缀区间应取正文末尾字节。
        /// </summary>
        [TestMethod]
        public void ApplyRangeHeader_Suffix_ReturnsTailBytes()
        {
            var response = CreateResponse(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

            response.ApplyRangeHeader("bytes=-3");

            Assert.AreEqual(HttpStatusCode.PartialContent, response.Status);
            CollectionAssert.AreEqual(new byte[] { 7, 8, 9 }, response.Body);
            Assert.AreEqual("bytes 7-9/10", Read(response, ContentRangeKey));
        }

        /// <summary>
        /// 起始位置超出正文长度时应回 416、写不可满足区间并清空正文。
        /// </summary>
        [TestMethod]
        public void ApplyRangeHeader_Unsatisfiable_Returns416()
        {
            var response = CreateResponse(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

            response.ApplyRangeHeader("bytes=100-");

            Assert.AreEqual(HttpStatusCode.RequestedRangeNotSatisfiable, response.Status);
            Assert.AreEqual("bytes */10", Read(response, ContentRangeKey));
            Assert.AreEqual("bytes", Read(response, AcceptRangesKey));
            Assert.IsTrue(response.Body == null || response.Body.Length == 0);
        }

        /// <summary>
        /// 末字节小于首字节属于非法区间，应回 200 全量。
        /// </summary>
        [TestMethod]
        public void ApplyRangeHeader_InvalidRange_KeepsFullBody()
        {
            var response = CreateResponse(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

            response.ApplyRangeHeader("bytes=5-3");

            Assert.AreEqual(HttpStatusCode.OK, response.Status);
            Assert.AreEqual(10, response.Body.Length);
            Assert.IsFalse(response.Headers.ContainsKey(ContentRangeKey));
        }

        /// <summary>
        /// 多区间不在支持范围内，应回 200 全量而不是 206。
        /// </summary>
        [TestMethod]
        public void ApplyRangeHeader_MultipleRanges_KeepsFullBody()
        {
            var response = CreateResponse(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

            response.ApplyRangeHeader("bytes=0-1,3-4");

            Assert.AreEqual(HttpStatusCode.OK, response.Status);
            Assert.AreEqual(10, response.Body.Length);
            Assert.IsFalse(response.Headers.ContainsKey(ContentRangeKey));
        }

        /// <summary>
        /// 非 200 响应（如 304）不参与 Range 改写，也不写 Accept-Ranges。
        /// </summary>
        [TestMethod]
        public void ApplyRangeHeader_NotModified_LeavesResponseUntouched()
        {
            var response = CreateResponse(new byte[0]);

            response.Status = HttpStatusCode.NotModified;

            response.ApplyRangeHeader("bytes=0-3");

            Assert.AreEqual(HttpStatusCode.NotModified, response.Status);
            Assert.IsFalse(response.Headers.ContainsKey(AcceptRangesKey));
            Assert.IsFalse(response.Headers.ContainsKey(ContentRangeKey));
        }

        /// <summary>
        /// 已启用 gzip 压缩时正文长度与 Content-Length 不一致，不启用 Range。
        /// </summary>
        [TestMethod]
        public void ApplyRangeHeader_Zipped_LeavesResponseUntouched()
        {
            var response = CreateResponse(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

            response.Init(null, null, "HTTP/1.1", true);

            response.ApplyRangeHeader("bytes=0-3");

            Assert.AreEqual(HttpStatusCode.OK, response.Status);
            Assert.AreEqual(10, response.Body.Length);
            Assert.IsFalse(response.Headers.ContainsKey(AcceptRangesKey));
            Assert.IsFalse(response.Headers.ContainsKey(ContentRangeKey));
        }

        private static HttpResponse CreateResponse(byte[] payload)
        {
            var response = (HttpResponse)Activator.CreateInstance(typeof(HttpResponse), true);

            response.Protocal = "HTTP/1.1";
            response.SetContent(payload);

            return response;
        }

        private static string Read(HttpResponse response, string key)
        {
            return response.Headers.TryGetValue(key, out var value) ? value : "<缺失>";
        }
    }
}