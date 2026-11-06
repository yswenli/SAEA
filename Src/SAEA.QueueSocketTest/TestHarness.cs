using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

using SAEA.Common;

namespace SAEA.QueueSocketTest
{
    /// <summary>
    /// 测试断言与结果汇总助手，统计通过、失败数量并统一输出
    /// </summary>
    public static class TestHarness
    {
        private static readonly object _lock = new object();
        private static int _pass;
        private static int _fail;
        private static readonly List<string> _failures = new List<string>();

        /// <summary>
        /// 已通过的断言数
        /// </summary>
        public static int PassCount { get { lock (_lock) { return _pass; } } }
        /// <summary>
        /// 已失败的断言数
        /// </summary>
        public static int FailCount { get { lock (_lock) { return _fail; } } }
        /// <summary>
        /// 累计断言数，等于通过数与失败数之和
        /// </summary>
        public static int TotalCount { get { lock (_lock) { return _pass + _fail; } } }
        /// <summary>
        /// 存在失败断言时为 true
        /// </summary>
        public static bool HasFailures { get { lock (_lock) { return _fail > 0; } } }

        /// <summary>
        /// 清零通过、失败计数与失败明细
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _pass = 0;
                _fail = 0;
                _failures.Clear();
            }
        }

        /// <summary>
        /// 记录一条断言结果，不带失败明细
        /// </summary>
        /// <param name="condition">断言条件，成立即记为通过</param>
        /// <param name="name">断言名称，用于输出与失败列表</param>
        /// <returns>原样返回断言条件的结果</returns>
        public static bool Expect(bool condition, string name)
        {
            return Expect(condition, name, null);
        }

        /// <summary>
        /// 记录一条断言结果，失败时把明细并入失败列表
        /// </summary>
        /// <param name="condition">断言条件，成立即记为通过</param>
        /// <param name="name">断言名称，用于输出与失败列表</param>
        /// <param name="detail">失败时输出的补充信息，为空则只记录名称</param>
        /// <returns>原样返回断言条件的结果</returns>
        public static bool Expect(bool condition, string name, string detail)
        {
            lock (_lock)
            {
                if (condition)
                {
                    _pass++;
                }
                else
                {
                    _fail++;
                    _failures.Add(string.IsNullOrEmpty(detail) ? name : name + " -> " + detail);
                }
            }

            if (condition)
            {
                ConsoleHelper.WriteLine("[PASS] " + name);
            }
            else
            {
                ConsoleHelper.WriteLine("[FAIL] " + name + (string.IsNullOrEmpty(detail) ? "" : " -> " + detail));
            }

            return condition;
        }

        /// <summary>
        /// 获取一个当前空闲的 TCP 端口，降低集成测试端口冲突概率。
        /// </summary>
        /// <returns>本机回环地址上当前空闲的端口号</returns>
        public static int GetFreeTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        /// <summary>
        /// 轮询等待条件成立，用于异步/网络测试，避免脆弱的固定延时。
        /// </summary>
        /// <param name="condition">待轮询的条件判断，返回 true 即结束等待</param>
        /// <param name="timeoutMs">最长等待时间，单位毫秒，默认 3000</param>
        /// <param name="pollMs">轮询间隔，单位毫秒，默认 25</param>
        /// <returns>超时前条件成立返回 true，否则返回超时后最后一次判断的结果</returns>
        public static async Task<bool> WaitUntil(Func<bool> condition, int timeoutMs = 3000, int pollMs = 25)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (condition())
                {
                    return true;
                }
                await Task.Delay(pollMs);
            }
            return condition();
        }

        /// <summary>
        /// 输出分节标题，用于分隔不同测试或基准的输出
        /// </summary>
        /// <param name="name">分节名称</param>
        public static void Section(string name)
        {
            ConsoleHelper.WriteLine("");
            ConsoleHelper.WriteLine("--- " + name + " ---");
        }

        /// <summary>
        /// 输出通过/失败统计，并逐条列出失败明细
        /// </summary>
        /// <param name="title">汇总标题，与统计结果一起输出</param>
        public static void WriteSummary(string title)
        {
            int pass;
            int total;
            int fail;
            List<string> failures;

            lock (_lock)
            {
                pass = _pass;
                total = _pass + _fail;
                fail = _fail;
                failures = new List<string>(_failures);
            }

            ConsoleHelper.WriteLine("");
            ConsoleHelper.WriteLine("=== " + title + ": " + pass + "/" + total + " passed, " + fail + " failed ===");

            if (fail > 0)
            {
                foreach (var failure in failures)
                {
                    ConsoleHelper.WriteLine("  FAILED: " + failure);
                }
            }
        }
    }
}
