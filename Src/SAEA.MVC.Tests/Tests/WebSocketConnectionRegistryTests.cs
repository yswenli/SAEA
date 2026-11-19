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
*文件名： WebSocketConnectionRegistryTests
*版本号： net10.0
*唯一标识：38532faf-2022-4127-a95b-4b3204a7c491
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 14:18:04
*描述：WebSocket 连接表清扫计时器释放的用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 14:18:04
*修改人： yswenli
*版本号： net10.0
*描述：新增
*
*****************************************************************************/
using System.Reflection;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.MVC.WebSockets;

namespace SAEA.MVC.Tests
{
    /// <summary>
    /// 连接表清扫计时器的用例：StopAll 必须真正释放计时器。
    /// </summary>
    [TestClass]
    public class WebSocketConnectionRegistryTests
    {
        /// <summary>
        /// StopAll 在清空连接表后应释放清扫计时器并置空字段，下一次 Register 才能重建。
        /// </summary>
        [TestMethod]
        public void WebSocketConnectionRegistry_StopAll_DisposesSweeper()
        {
            // _sweeper 仍是 private static，测试不改生产可见性，只通过反射读取
            var field = typeof(WebSocketConnectionRegistry).GetField("_sweeper", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(field, "应能通过反射取到私有静态字段 _sweeper");

            WebSocketConnectionRegistry.EnsureSweeper();

            var timer = field.GetValue(null) as Timer;

            Assert.IsNotNull(timer, "EnsureSweeper 之后应已创建清扫计时器");

            WebSocketConnectionRegistry.StopAll();

            Assert.IsNull(field.GetValue(null), "StopAll 之后 _sweeper 应置为 null");

            // 现代 .NET 对已释放的 Timer 调 Change 只返回 false 而不抛 ObjectDisposedException，
            // 用返回值判定才能证明计时器真的被 Dispose，而不只是字段被置空
            Assert.IsFalse(timer.Change(Timeout.Infinite, Timeout.Infinite), "计时器应已被 Dispose，而不是只把字段置空");
        }
    }
}