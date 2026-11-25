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
*命名空间：SAEA.Sockets.Pipeline.Tests
*文件名： DefaultChannelDrainSpecTests
*版本号： net10.0
*唯一标识：7b1c4e52-0f68-4d39-9b71-2c85d0e4a763
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/24 16:59:19
*描述：连接级排空规格的设置、取走与失效边界用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/24 18:21:25
*修改人： yswenli
*版本号： net10.0
*描述：排空字节断言改成长整型
*
*****************************************************************************/
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SAEA.Sockets.Pipeline.Tests
{
    /// <summary>
    /// 排空规格用例：只验证规格的保存与一次性取走语义，不依赖传输层。
    /// </summary>
    [TestClass]
    public class DefaultChannelDrainSpecTests
    {
        /// <summary>
        /// 设置有效规格后能取到原值，且第二次取走返回 false，保证同一次收口只排空一次。
        /// </summary>
        [TestMethod]
        public void SetDrainSpec_有效规格_只能取走一次()
        {
            using (var channel = new DefaultChannel("drain-once", null))
            {
                channel.SetDrainSpec(128, 500);

                long drainBytes;
                int timeoutMs;
                Assert.IsTrue(channel.TryTakeDrainSpec(out drainBytes, out timeoutMs), "有效规格应能取到。");
                Assert.AreEqual(128L, drainBytes, "排空字节数应与设置值一致。");
                Assert.AreEqual(500, timeoutMs, "排空超时应与设置值一致。");

                Assert.IsFalse(
                    channel.TryTakeDrainSpec(out drainBytes, out timeoutMs),
                    "规格取走后应清空，避免收口重复排空。");
            }
        }

        /// <summary>
        /// 任一参数非正时忽略设置；这与「不排空」语义一致，不能让收口误判为需要等待。
        /// </summary>
        [TestMethod]
        public void SetDrainSpec_参数非正_忽略()
        {
            using (var channel = new DefaultChannel("drain-invalid", null))
            {
                channel.SetDrainSpec(0, 500);
                channel.SetDrainSpec(128, 0);

                long drainBytes;
                int timeoutMs;
                Assert.IsFalse(
                    channel.TryTakeDrainSpec(out drainBytes, out timeoutMs),
                    "参数非正的设置不应留下排空规格。");
            }
        }

        /// <summary>
        /// 从未设置过规格时取走返回 false，并给出 0 值。
        /// </summary>
        [TestMethod]
        public void TryTakeDrainSpec_未设置_返回false()
        {
            using (var channel = new DefaultChannel("drain-none", null))
            {
                long drainBytes;
                int timeoutMs;
                Assert.IsFalse(
                    channel.TryTakeDrainSpec(out drainBytes, out timeoutMs),
                    "未设置规格时不应返回 true。");
                Assert.AreEqual(0L, drainBytes, "未设置时排空字节数应为 0。");
                Assert.AreEqual(0, timeoutMs, "未设置时排空超时应为 0。");
            }
        }
    }
}