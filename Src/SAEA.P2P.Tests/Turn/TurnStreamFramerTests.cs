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
 *命名空间：SAEA.P2P.Tests.Turn
 *文件名： TurnStreamFramerTests
 *版本号： net10.0
 *唯一标识：610aae13-6659-4be1-bed7-0d6797abdd80
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/12 01:30:00
 *描述：TURN over TCP 流式分帧器的半包、粘包与重同步验收
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/12 01:30:00
 *修改人： yswenli
 *版本号： net10.0
 *描述：新增流式分帧器半包、粘包与重同步用例
 *
 *****************************************************************************/
using System;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.P2P.Stun;
using SAEA.P2P.Turn;

namespace SAEA.P2P.Tests.Turn
{
    /// <summary>
    /// 流式分帧器：半包不产出且保留状态、一次读取含多条报文时逐条产出、
    /// 非法首字节丢弃一个字节后继续扫描。
    /// </summary>
    [TestCategory(P2PTestSuite.Unit)]
    [TestClass]
    public class TurnStreamFramerTests
    {
        /// <summary>
        /// 一条 STUN 报文拆成两次投喂时，第一次不产出，补足后产出原始字节
        /// </summary>
        [TestMethod]
        public void TurnStreamFramer_半包_不产出消息()
        {
            var framer = new TurnStreamFramer();
            var message = new StunMessage(StunMessageType.Allocate, StunClass.Request).ToArray();
            var split = message.Length / 2;

            framer.Feed(new ReadOnlySpan<byte>(message, 0, split));

            TurnStreamFrame frame;
            Assert.IsFalse(framer.TryReadFrame(out frame), "不足一条完整报文时不应产出帧");
            Assert.IsNull(frame, "未取到帧时不应给出帧对象");
            Assert.AreEqual(split, framer.BufferedCount, "半包字节应留在缓冲中等待后续数据");

            framer.Feed(new ReadOnlySpan<byte>(message, split, message.Length - split));

            Assert.IsTrue(framer.TryReadFrame(out frame), "补足后应产出完整报文");
            Assert.AreEqual(TurnStreamFrameKind.Stun, frame.Kind, "首字节 0x00 到 0x3F 应按 STUN 解析");
            CollectionAssert.AreEqual(message, frame.Data, "产出的字节应与输入完全一致");
            Assert.AreEqual(0, framer.BufferedCount, "取走整条报文后缓冲应为空");
        }

        /// <summary>
        /// 一次投喂里含一条 STUN 与一条 ChannelData 时，应先后各产出一条
        /// </summary>
        [TestMethod]
        public void TurnStreamFramer_粘包_产出两条()
        {
            const ushort channelNumber = 0x4001;

            var framer = new TurnStreamFramer();
            var stun = new StunMessage(StunMessageType.Refresh, StunClass.Request).ToArray();
            var payload = Encoding.UTF8.GetBytes("sticky");
            var channelData = ChannelData.Encode(channelNumber, payload, true);

            var combined = new byte[stun.Length + channelData.Length];
            Buffer.BlockCopy(stun, 0, combined, 0, stun.Length);
            Buffer.BlockCopy(channelData, 0, combined, stun.Length, channelData.Length);

            framer.Feed(combined);

            TurnStreamFrame first;
            Assert.IsTrue(framer.TryReadFrame(out first), "第一条应可立即取出");
            Assert.AreEqual(TurnStreamFrameKind.Stun, first.Kind, "先取出应先投喂的 STUN 报文");
            CollectionAssert.AreEqual(stun, first.Data, "STUN 帧字节应与输入一致");

            TurnStreamFrame second;
            Assert.IsTrue(framer.TryReadFrame(out second), "第二条应可继续取出");
            Assert.AreEqual(TurnStreamFrameKind.ChannelData, second.Kind, "后一条应按 ChannelData 解析");
            CollectionAssert.AreEqual(channelData, second.Data, "ChannelData 帧字节应含头部与补齐");

            TurnStreamFrame extra;
            Assert.IsFalse(framer.TryReadFrame(out extra), "两条取完后不应再产出帧");
            Assert.AreEqual(0, framer.BufferedCount, "全部取完后缓冲应为空");
        }

        /// <summary>
        /// 首字节落在 0x50 到 0xFF 时丢弃该字节，后续合法帧仍能取出
        /// </summary>
        [TestMethod]
        public void TurnStreamFramer_首字节非法_丢弃并继续()
        {
            const ushort channelNumber = 0x4002;

            var framer = new TurnStreamFramer();
            var noise = new byte[] { 0x50, 0x7F, 0xC0 };
            framer.Feed(noise);

            TurnStreamFrame frame;
            Assert.IsFalse(framer.TryReadFrame(out frame), "只有非法首字节时不应产出帧");
            Assert.AreEqual(0, framer.BufferedCount, "非法字节应被逐个丢弃");

            var payload = Encoding.UTF8.GetBytes("resync");
            var channelData = ChannelData.Encode(channelNumber, payload, true);
            framer.Feed(channelData);

            Assert.IsTrue(framer.TryReadFrame(out frame), "重同步后应能取出合法帧");
            Assert.AreEqual(TurnStreamFrameKind.ChannelData, frame.Kind, "合法帧应按 ChannelData 解析");
            CollectionAssert.AreEqual(channelData, frame.Data, "取出的字节应与输入一致");
            Assert.AreEqual(0, framer.BufferedCount, "取走后不应残留字节");
        }
    }
}