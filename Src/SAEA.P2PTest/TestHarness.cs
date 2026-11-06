using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

using SAEA.Common;

namespace SAEA.P2PTest
{
    /// <summary>
    /// 自测程序的轻量断言与结果统计工具，不依赖单元测试框架
    /// </summary>
    public static class TestHarness
    {
        private static readonly object _lock = new object();
        private static int _pass;
        private static int _fail;
        private static readonly List<string> _failures = new List<string>();

        /// <summary>
        /// 已通过的断言条数
        /// </summary>
        public static int PassCount { get { lock (_lock) { return _pass; } } }
        /// <summary>
        /// 已失败的断言条数
        /// </summary>
        public static int FailCount { get { lock (_lock) { return _fail; } } }
        /// <summary>
        /// 累计断言条数，为通过数与失败数之和
        /// </summary>
        public static int TotalCount { get { lock (_lock) { return _pass + _fail; } } }
        /// <summary>
        /// 是否存在失败断言，供进程退出码判断
        /// </summary>
        public static bool HasFailures { get { lock (_lock) { return _fail > 0; } } }

        /// <summary>
        /// 清零通过数、失败数与失败明细，供每个测试套件开始时重置统计
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
        /// 断言条件成立并计数，不带失败明细
        /// </summary>
        /// <param name="condition">断言条件</param>
        /// <param name="name">断言名称，用于输出与失败汇总</param>
        /// <returns>条件成立返回 true</returns>
        public static bool Expect(bool condition, string name)
        {
            return Expect(condition, name, null);
        }

        /// <summary>
        /// 断言条件成立并计数，失败时记录名称与明细
        /// </summary>
        /// <param name="condition">断言条件</param>
        /// <param name="name">断言名称，用于输出与失败汇总</param>
        /// <param name="detail">失败明细，为空时只记录名称</param>
        /// <returns>条件成立返回 true</returns>
        public static bool Expect(bool condition, string name, string? detail)
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
                    _failures.Add(string.IsNullOrEmpty(detail) ? name : $"{name} -> {detail}");
                }
            }

            if (condition)
            {
                ConsoleHelper.WriteLine($"[PASS] {name}");
            }
            else
            {
                ConsoleHelper.WriteLine($"[FAIL] {name}{(string.IsNullOrEmpty(detail) ? "" : " -> " + detail)}");
            }

            return condition;
        }

        /// <summary>
        /// 断言执行 action 时抛出指定异常并按断言结果计数
        /// </summary>
        /// <typeparam name="TException">期望抛出的异常类型</typeparam>
        /// <param name="action">被检查的委托</param>
        /// <param name="name">断言名称，用于输出与失败汇总</param>
        /// <returns>捕获到的异常实例；未抛出或类型不符时返回 null</returns>
        public static TException? Throws<TException>(Action action, string name) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException ex)
            {
                Expect(true, name);
                return ex;
            }
            catch (Exception ex)
            {
                Expect(false, name, $"threw {ex.GetType().Name} instead of {typeof(TException).Name}: {ex.Message}");
                return null;
            }

            Expect(false, name, "no exception was thrown");
            return null;
        }

        /// <summary>
        /// 获取一个当前空闲的 TCP 端口，降低集成测试端口冲突概率。
        /// </summary>
        /// <returns>本机回环地址上当前空闲的 TCP 端口号</returns>
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
        /// 获取一个当前空闲的 UDP 端口，降低集成测试端口冲突概率（UDP 端口空间与 TCP 独立）。
        /// </summary>
        /// <returns>本机回环地址上当前空闲的 UDP 端口号</returns>
        public static int GetFreeUdpPort()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                return ((IPEndPoint)socket.LocalEndPoint).Port;
            }
        }

        /// <summary>
        /// 轮询等待条件成立，用于异步/网络测试，避免脆弱的固定延时。
        /// </summary>
        /// <param name="condition">待等待的条件委托</param>
        /// <param name="timeoutMs">超时毫秒数，默认 3000</param>
        /// <param name="pollMs">轮询间隔毫秒数，默认 25</param>
        /// <returns>超时内条件成立返回 true；超时后再判定一次，仍不成立返回 false</returns>
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
        /// 输出分组标题，便于在控制台区分各测试段落
        /// </summary>
        /// <param name="name">段落名称</param>
        public static void Section(string name)
        {
            ConsoleHelper.WriteLine("");
            ConsoleHelper.WriteLine($"--- {name} ---");
        }

        /// <summary>
        /// 输出测试汇总行，并在有失败时逐条打印失败明细
        /// </summary>
        /// <param name="title">汇总标题，通常为测试套件名</param>
        public static void WriteSummary(string title)
        {
            ConsoleHelper.WriteLine("");
            ConsoleHelper.WriteLine($"=== {title}: {PassCount}/{TotalCount} passed, {FailCount} failed ===");

            if (_fail > 0)
            {
                foreach (var failure in _failures)
                {
                    ConsoleHelper.WriteLine($"  FAILED: {failure}");
                }
            }
        }
    }
}