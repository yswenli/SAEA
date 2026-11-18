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
 *文件名： HttpSessionSweepTests
 *版本号： net10.0
 *唯一标识：b7c304ed-d1e0-4b5c-8861-1df87e9c6caf
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/11/18 13:26:01
 *描述：会话表过期清理的行为用例：过期条目全部移除、未过期条目保留
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/18 13:26:01
 *修改人： yswenli
 *版本号： net10.0
 *描述：新增，锁定清理一轮移除所有过期会话且不误删未过期会话
 *
 *****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SAEA.Http.Tests
{
    /// <summary>
    /// 会话表清理用例：<c>HttpSessionManager</c> 是 internal，测试通过反射调用，不扩大产品公开面。
    /// </summary>
    [TestClass]
    public class HttpSessionSweepTests
    {
        private static readonly Type ManagerType =
            typeof(WebHost).Assembly.GetType("SAEA.Http.HttpSessionManager", true);

        /// <summary>
        /// 一轮清理应移除所有过期会话（不是只处理第一个），并原样保留未过期会话。
        /// </summary>
        [TestMethod]
        public void HttpSessionManager_SweepExpired_RemovesAllExpiredAndKeepsLive()
        {
            var cache = GetCache();

            var expiredId = "saea-sweep-expired-" + Guid.NewGuid().ToString("N");

            var liveId = "saea-sweep-live-" + Guid.NewGuid().ToString("N");

            var expired = GetSession(expiredId);

            var live = GetSession(liveId);

            expired["key"] = "value";

            live["key"] = "value";

            expired.Expired = DateTime.Now.AddMinutes(-1);

            try
            {
                SweepExpired();

                Assert.IsFalse(cache.ContainsKey(expiredId), "过期会话必须从会话表移除，否则会话表会无界增长。");
                Assert.IsTrue(cache.ContainsKey(liveId), "未过期会话不得被清理。");
                Assert.AreEqual(0, expired.Dictionary.Count, "移除前必须先清空会话内容。");
                Assert.AreEqual(1, live.Dictionary.Count, "未过期会话的内容不得被清空。");

                // 过期条目已清空，后续两轮都应没有可移除的条目。
                Assert.AreEqual(0, SweepExpired(), "清理完成后不应再有可移除的会话。");
                Assert.AreEqual(0, SweepExpired(), "第二轮清理同样应返回 0。");
            }
            finally
            {
                RemoveSession(expiredId);
                RemoveSession(liveId);
            }
        }

        /// <summary>从管理器实例字段取会话表。</summary>
        private static ConcurrentDictionary<string, HttpSession> GetCache()
        {
            var field = ManagerType.GetField("_cache", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(field, "HttpSessionManager 应保持 _cache 字段，否则本用例需随实现更新。");

            return (ConcurrentDictionary<string, HttpSession>)field.GetValue(null);
        }

        /// <summary>按 id 取会话，不存在时创建；与产品入口走同一条路径。</summary>
        private static HttpSession GetSession(string id)
        {
            var method = ManagerType.GetMethod("GetIfNotExistsSet", BindingFlags.Public | BindingFlags.Static);

            Assert.IsNotNull(method, "HttpSessionManager 应保持 GetIfNotExistsSet 入口。");

            return (HttpSession)method.Invoke(null, new object[] { id });
        }

        /// <summary>执行一轮过期清理并返回移除数量。</summary>
        private static int SweepExpired()
        {
            var method = ManagerType.GetMethod("SweepExpired", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method, "HttpSessionManager 应提供 SweepExpired 供清理线程与用例共用。");

            return (int)method.Invoke(null, null);
        }

        /// <summary>移除用例创建的会话，避免污染同一进程内的其它用例。</summary>
        private static void RemoveSession(string id)
        {
            var method = ManagerType.GetMethod("Remove", BindingFlags.Public | BindingFlags.Static);

            if (method != null)
                method.Invoke(null, new object[] { id });
        }
    }
}