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
 *命名空间：SAEA.MVC.Tests.Tests
 *文件名： UserTokenRetireTests
 *版本号： net10.0
 *唯一标识：3c0b6f2e-8f7a-4d15-9c61-2b7d4a5e9f13
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/13 16:52:10
 *描述：令牌退役与缓冲区按需扩容的用例
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/13 16:52:10
 *修改人： yswenli
 *版本号： net10.0
 *描述：新增，覆盖退役令牌不入池、缓冲不归还、在途计数不减与扩容后槽位映射
 *
 *****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.Sockets.Base;
using SAEA.Sockets.Core;

namespace SAEA.MVC.Tests.Tests
{
    /// <summary>
    /// 发送看门狗放弃路径的支撑能力：令牌退役后永不复用，缓冲区槽位在硬上限内按需扩容。
    /// </summary>
    [TestClass]
    public class UserTokenRetireTests
    {
        /// <summary>
        /// 初始槽位数。分段大小为 1MB，4KB 缓冲区对应每段 256 个槽位，
        /// 300 个槽位会留下一个只有 44 个槽位的短段，用于暴露扩容时的槽位越界。
        /// </summary>
        private const int InitialSlots = 300;

        /// <summary>
        /// 单个缓冲区大小。
        /// </summary>
        private const int BufferSize = 4096;

        /// <summary>
        /// 槽位耗尽后按整段扩容，且新槽位必须落在新段内：首段 256 个、短段 44 个、
        /// 扩容新增 88 个，共 388 个可分配槽位；再申请应因触达扩容上限而失败。
        /// </summary>
        [TestMethod]
        public void BufferManager_SlotExhausted_GrowsToHardLimitAndKeepsSlotMapping()
        {
            // BufferManager 是内部类型，只能通过反射驱动：它是退役补建的兜底，公开 API 触发不到
            var managerType = typeof(UserTokenPool).Assembly.GetType("SAEA.Sockets.Core.BufferManager", true);
            var setBuffer = managerType.GetMethod("SetBuffer", BindingFlags.Instance | BindingFlags.Public);
            var freeBuffer = managerType.GetMethod("FreeBuffer", BindingFlags.Instance | BindingFlags.Public);
            var dispose = managerType.GetMethod("Dispose", BindingFlags.Instance | BindingFlags.Public);

            Assert.IsNotNull(setBuffer);
            Assert.IsNotNull(freeBuffer);
            Assert.IsNotNull(dispose);

            var manager = Activator.CreateInstance(managerType, new object[] { (long)BufferSize * InitialSlots, BufferSize });
            var allocated = new List<SocketAsyncEventArgs>();

            try
            {
                for (var i = 0; i < 388; i++)
                {
                    var args = new SocketAsyncEventArgs();

                    Assert.IsTrue((bool)setBuffer.Invoke(manager, new object[] { args }), $"第 {i + 1} 次分配失败");

                    Assert.IsNotNull(args.Buffer);
                    // 段按偏移切片，越界说明扩容把槽位算进了不够长的短段
                    Assert.IsTrue(args.Buffer.Length - args.Offset >= BufferSize,
                        $"第 {i + 1} 次分配的偏移越界：offset={args.Offset}，segment={args.Buffer.Length}");

                    allocated.Add(args);
                }

                var overflow = new SocketAsyncEventArgs();

                Assert.IsFalse((bool)setBuffer.Invoke(manager, new object[] { overflow }), "扩容上限之后仍能分配槽位");
            }
            finally
            {
                foreach (var args in allocated)
                    freeBuffer.Invoke(manager, new object[] { args });

                dispose.Invoke(manager, null);
            }
        }

        /// <summary>
        /// 退役令牌不入池、不归还池化缓冲、不被清零在途计数；核销的创建计数在下次取用时补建新令牌。
        /// </summary>
        [TestMethod]
        public void UserTokenPool_RetireToken_NeverEnqueuedAndCapacityRebuilt()
        {
            using (var pool = new UserTokenPool(new BaseContext<BaseCoder>(), 2, BufferSize, null))
            {
                var retired = pool.Dequeue(1000);
                var survivor = pool.Dequeue(1000);

                Assert.IsNotNull(retired);
                Assert.IsNotNull(survivor);
                Assert.AreNotSame(retired, survivor);

                var baseToken = (BaseUserToken)retired;

                // 模拟一条未收口的在途操作：退役只标记状态，不得把计数清零
                Assert.IsTrue(InvokeInternal<bool>(baseToken, "TryBeginOperation"));
                var stateBeforeRetire = ReadInternal<int>(baseToken, "OperationState");

                var retire = typeof(UserTokenPool).GetMethod("Retire", BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.IsNotNull(retire);
                Assert.IsTrue((bool)retire.Invoke(pool, new object[] { retired }));
                Assert.IsFalse((bool)retire.Invoke(pool, new object[] { retired }), "重复退役不得再次核销容量");
                Assert.AreEqual(stateBeforeRetire, ReadInternal<int>(baseToken, "OperationState"), "退役不得改变在途计数");

                // 内核可能仍在读该缓冲：入池或清空缓冲都会让新连接踩到同一块内存
                var buffer = retired.ReadArgs.Buffer;

                Assert.IsFalse(pool.Enqueue(retired), "退役令牌不得入池");
                Assert.AreSame(buffer, retired.ReadArgs.Buffer, "退役令牌的缓冲不得被清空");
                Assert.AreSame(retired, retired.ReadArgs.UserToken);

                Assert.IsTrue(pool.Enqueue(survivor));
                Assert.AreSame(survivor, pool.Dequeue(1000), "已入池令牌应优先复用");

                // 退役核销了创建计数：池中已无可用令牌，必须补建而不是返回退役令牌
                var rebuilt = pool.Dequeue(1000);

                Assert.IsNotNull(rebuilt);
                Assert.AreNotSame(retired, rebuilt);
                Assert.AreNotSame(survivor, rebuilt);
                Assert.IsNotNull(rebuilt.ReadArgs.Buffer, "补建的令牌应分配到缓冲区");
            }
        }

        /// <summary>
        /// 调用被测类型的内部方法。
        /// </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="target">目标实例</param>
        /// <param name="methodName">内部方法名</param>
        /// <returns>方法返回值</returns>
        private static T InvokeInternal<T>(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(method, "找不到内部方法：" + methodName);

            return (T)method.Invoke(target, null);
        }

        /// <summary>
        /// 读取被测类型的内部属性。
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="target">目标实例</param>
        /// <param name="propertyName">内部属性名</param>
        /// <returns>属性值</returns>
        private static T ReadInternal<T>(object target, string propertyName)
        {
            var field = target.GetType().GetField(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);

            if (field != null)
                return (T)field.GetValue(target);

            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(property, "找不到内部成员：" + propertyName);

            return (T)property.GetValue(target);
        }
    }
}