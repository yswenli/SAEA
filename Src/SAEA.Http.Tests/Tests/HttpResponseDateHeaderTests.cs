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
*文件名： HttpResponseDateHeaderTests
*版本号： net10.0
*唯一标识：6ff16f39-9ab9-4968-b12d-1542a090f015
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/20 17:10:00
*描述：Date 报头按 UTC 输出的端到端用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/20 17:10:00
*修改人： yswenli
*版本号： net10.0
*描述：新建用例，校验 Date 头写出的 GMT 文本是 UTC 时刻而不是本地时钟值
*
*****************************************************************************/
using System;
using System.Globalization;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SAEA.Http.Tests
{
    /// <summary>
    /// Date 报头用例：报头里的 GMT 文本必须是 UTC 时刻，不能是带 GMT 后缀的本地时钟值。
    /// </summary>
    [TestClass]
    public class HttpResponseDateHeaderTests
    {
        /// <summary>
        /// Date 头解析出的 UTC 时刻应落在当前 UTC 时刻的 ±60 秒内。
        /// 修复前在 UTC+8 机器上会差 8 小时，因此该断言在非 UTC 机器上就是有效判据。
        /// </summary>
        [TestMethod]
        public void HttpResponse_DateHeader_ParsedAsUtcWithinOneMinute()
        {
            var response = (HttpResponse)Activator.CreateInstance(typeof(HttpResponse), true);

            var header = BuildHeaderText(response);

            var dateText = ReadHeaderValue(header, "Date");

            Assert.AreNotEqual("<缺失>", dateText, "响应报头必须含 Date 字段。");

            var parsed = DateTime.ParseExact(dateText, "r", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

            var offset = (parsed - DateTime.UtcNow).TotalSeconds;

            Assert.IsTrue(Math.Abs(offset) <= 60,
                "Date 应为 UTC 时刻，实际偏差 " + offset + " 秒（修复前在 UTC+8 机器上会差 8 小时），报头值：" + dateText);
        }

        /// <summary>
        /// 响应的构造函数是 internal，测试通过反射调用受保护的报头构建方法，不扩大产品公开面。
        /// </summary>
        /// <param name="response">响应实例</param>
        /// <returns>完整报头文本，每行以换行结束</returns>
        private static string BuildHeaderText(HttpResponse response)
        {
            var buildHeader = typeof(HttpResponse).GetMethod("BuildHeader", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(buildHeader, "BuildHeader 应存在，否则本用例失去意义。");

            return (string)buildHeader.Invoke(response, null);
        }

        /// <summary>
        /// 从报头文本里取指定字段的值，名字不区分大小写，值已去掉首尾空白。
        /// </summary>
        /// <param name="header">完整报头文本</param>
        /// <param name="name">字段名</param>
        /// <returns>字段值，不存在时返回「&lt;缺失&gt;」</returns>
        private static string ReadHeaderValue(string header, string name)
        {
            var lines = header.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var index = line.IndexOf(':');

                if (index <= 0)
                    continue;

                if (line.Substring(0, index).Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                    return line.Substring(index + 1).Trim();
            }

            return "<缺失>";
        }
    }
}