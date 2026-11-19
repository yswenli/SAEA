/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http
*文件名： HttpSessionManager
*版本号： v26.4.23.1
*唯一标识：6db18cd0-7e38-4127-b674-fcbaebb6a3b2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/04/17 18:23:22
*描述：HttpSessionManager接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 18:45:54
*修改人： yswenli
*版本号： v26.11.16.1
*描述：会话 ID 改为逐字节查十六进制表拼装，去掉 string.Join 的装箱枚举与中间字符串
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Threading;

using SAEA.Common;
using SAEA.Common.Threading;

namespace SAEA.Http
{
    /// <summary>
    /// HTTP 会话管理器，负责会话的创建与过期清理
    /// </summary>
    internal static class HttpSessionManager
    {
        // 十六进制字符表：逐字节按低 4 位取值，避免 decimal 拼接时的装箱枚举与中间字符串。
        private const string HexChars = "0123456789abcdef";

        static ConcurrentDictionary<string, HttpSession> _cache = new ConcurrentDictionary<string, HttpSession>();

        static Random _random = new Random();

        /// <summary>
        /// 静态构造函数，启动过期会话的定期清理任务
        /// </summary>
        static HttpSessionManager()
        {
            TaskHelper.LongRunning(() =>
            {
                while (true)
                {
                    try
                    {
                        SweepExpired();
                    }
                    catch (Exception ex)
                    {
                        // 清理线程必须长期存活：单轮异常只记录，不能让整个会话表从此无人回收。
                        LogHelper.Debug("HTTP 会话清理失败:" + ex.Message);
                    }

                    Thread.Sleep(1000);
                }
            });
        }

        /// <summary>
        /// 清理一轮已过期会话：逐个清空内容后再从字典移除，返回本轮移除的会话数。
        /// <para>
        /// 先清内容再移除，避免在途请求读到半移除的会话；<see cref="ConcurrentDictionary{TKey,TValue}"/>
        /// 的枚举是快照，期间被其它线程移除时 <c>TryRemove</c> 返回 false，属正常情况。
        /// </para>
        /// </summary>
        /// <returns>本轮实际移除的会话数</returns>
        internal static int SweepExpired()
        {
            var removed = 0;

            foreach (var item in _cache)
            {
                if (item.Value.Expired >= DateTimeHelper.Now)
                    continue;

                // 先清内容再移除：ConcurrentDictionary 的枚举是快照，期间被其它线程移除时
                // TryRemove 返回 false，属正常情况，不计入本轮移除数。
                item.Value.Clear();

                if (_cache.TryRemove(item.Key, out HttpSession _))
                    removed++;
            }

            return removed;
        }

        /// <summary>
        /// 生成sessionID
        /// </summary>
        /// <returns>15 位十六进制字符串</returns>
        public static string GeneratID()
        {
            var bytes = new byte[15];

            _random.NextBytes(bytes);

            var chars = new char[15];

            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = HexChars[bytes[i] & 0x0F];
            }

            return new string(chars);
        }

        /// <summary>
        /// 获取HttpSession
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static HttpSession GetIfNotExistsSet(string id)
        {
            var session = _cache.GetOrAdd(id, (k) =>
            {
                return new HttpSession(k);
            });
            session.Refresh();
            return session;
        }

        /// <summary>
        /// 移除httpSession
        /// </summary>
        /// <param name="id"></param>
        public static void Remove(string id)
        {
            if (_cache.TryRemove(id, out HttpSession httpSession))
            {
                httpSession.Dispose();
            }
        }

    }
}