using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

using SAEA.Common;

namespace SAEA.QueueSocketTest
{
    public static class TestHarness
    {
        private static readonly object _lock = new object();
        private static int _pass;
        private static int _fail;
        private static readonly List<string> _failures = new List<string>();

        public static int PassCount { get { lock (_lock) { return _pass; } } }
        public static int FailCount { get { lock (_lock) { return _fail; } } }
        public static int TotalCount { get { lock (_lock) { return _pass + _fail; } } }
        public static bool HasFailures { get { lock (_lock) { return _fail > 0; } } }

        public static void Reset()
        {
            lock (_lock)
            {
                _pass = 0;
                _fail = 0;
                _failures.Clear();
            }
        }

        public static bool Expect(bool condition, string name)
        {
            return Expect(condition, name, null);
        }

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

        public static void Section(string name)
        {
            ConsoleHelper.WriteLine("");
            ConsoleHelper.WriteLine("--- " + name + " ---");
        }

        public static void WriteSummary(string title)
        {
            ConsoleHelper.WriteLine("");
            ConsoleHelper.WriteLine("=== " + title + ": " + PassCount + "/" + TotalCount + " passed, " + FailCount + " failed ===");

            if (_fail > 0)
            {
                foreach (var failure in _failures)
                {
                    ConsoleHelper.WriteLine("  FAILED: " + failure);
                }
            }
        }
    }
}