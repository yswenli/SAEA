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
*文件名： DateTimeHelperTests
*版本号： net10.0
*唯一标识：4cdd9188-020a-4304-999a-52b379a7ec45
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/20 17:10:00
*描述：ToGMTString 时区换算的单元测试
*
*=====================================================================
*修改标记
*修改时间：2026/11/20 17:10:00
*修改人： yswenli
*版本号： net10.0
*描述：新建用例，锁定 ToGMTString 一律输出 UTC 时刻而不是本地时钟值
*
*****************************************************************************/
using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SAEA.Common.Tests
{
    /// <summary>
    /// ToGMTString 时区换算用例：报头时间一律按 UTC 输出，不能把本地时钟值直接配上 GMT 后缀。
    /// </summary>
    [TestClass]
    public class DateTimeHelperTests
    {
        /// <summary>
        /// 同一时刻的 Utc 值与对应本地时间值必须写出完全相同的 UTC 文本。
        /// 本机时区为 UTC 时这两个值本就相同，该用例无法区分修复前后；它在非 UTC 机器上才是有效判据。
        /// </summary>
        [TestMethod]
        public void ToGMTString_SameInstantUtcAndLocal_ProduceSameUtcText()
        {
            var utc = new DateTime(2026, 11, 20, 8, 10, 0, DateTimeKind.Utc);

            var local = utc.ToLocalTime();

            const string expected = "Fri, 20 Nov 2026 08:10:00 GMT";

            Assert.AreEqual(expected, utc.ToGMTString(), "Kind 为 Utc 时应原样输出 UTC 时钟值。");
            Assert.AreEqual(expected, local.ToGMTString(), "Kind 为 Local 时必须先换算到 UTC，再追加 GMT 后缀。");
        }

        /// <summary>
        /// Kind 为 Unspecified 的值按 .NET 约定视为本地时间：换算结果必须与同钟面的 Local 值一致，
        /// 且等于按本机时区偏移独立算出的 UTC 钟面文本。
        /// 本机时区为 UTC 时修复前后输出相同，该用例在非 UTC 机器上才能暴露差异。
        /// </summary>
        [TestMethod]
        public void ToGMTString_UnspecifiedKind_TreatedAsLocalTime()
        {
            var unspecified = new DateTime(2026, 11, 20, 16, 10, 0, DateTimeKind.Unspecified);

            var local = DateTime.SpecifyKind(unspecified, DateTimeKind.Local);

            Assert.AreEqual(local.ToGMTString(), unspecified.ToGMTString(), "Unspecified 必须按 DateTime.ToUniversalTime 的约定视为本地时间。");

            // 按本机时区偏移独立算出该钟面对应的 UTC 时刻，避免把 UTC+8 这类环境细节写死进断言
            var utcInstant = new DateTimeOffset(unspecified, TimeZoneInfo.Local.GetUtcOffset(unspecified)).UtcDateTime;

            Assert.AreEqual(utcInstant.ToGMTString(), unspecified.ToGMTString(), "Unspecified 换算后必须等于对应 UTC 时刻的 GMT 文本。");
        }
    }
}