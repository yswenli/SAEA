using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace LoadTestTool
{
    class Program
    {
        // 统计信息类
        class TestStats
        {
            public int SuccessCount { get; set; }
            public int FailedCount { get; set; }
            public long TotalResponseTime { get; set; }
        }

        static async Task Main(string[] args)
        {
            Console.WriteLine("SAEA.MVCTest API 压测工具");
            Console.WriteLine(new string('=', 50));

            // 压测配置
            string url = "http://127.0.0.1:28080/home/ping";
            int concurrentCount = 10; // 并发数
            int totalRequests = 100; // 总请求数

            Console.WriteLine($"压测目标: {url}");
            Console.WriteLine($"并发数: {concurrentCount}");
            Console.WriteLine($"总请求数: {totalRequests}");
            Console.WriteLine(new string('=', 50));

            // 初始化HttpClient
            using (var httpClient = new HttpClient())
            {
                var stopwatch = Stopwatch.StartNew();
                var stats = new TestStats();

                // 创建任务数组
                var tasks = new Task[totalRequests];

                // 发送请求
                for (int i = 0; i < totalRequests; i++)
                {
                    tasks[i] = SendRequest(httpClient, url, stats);
                    
                    // 控制并发数
                    if ((i + 1) % concurrentCount == 0)
                    {
                        await Task.WhenAll(tasks.Skip(i - concurrentCount + 1).Take(concurrentCount));
                    }
                }

                // 等待所有任务完成
                await Task.WhenAll(tasks);

                stopwatch.Stop();

                // 输出结果
                Console.WriteLine("\n压测结果:");
                Console.WriteLine(new string('=', 50));
                Console.WriteLine($"总耗时: {stopwatch.ElapsedMilliseconds} ms");
                Console.WriteLine($"成功请求: {stats.SuccessCount}");
                Console.WriteLine($"失败请求: {stats.FailedCount}");
                Console.WriteLine($"成功率: {(double)stats.SuccessCount / totalRequests * 100:F2}%");
                if (stats.SuccessCount > 0)
                {
                    Console.WriteLine($"平均响应时间: {stats.TotalResponseTime / stats.SuccessCount} ms");
                }
                Console.WriteLine($"QPS: {(double)totalRequests / stopwatch.ElapsedMilliseconds * 1000:F2}");
            }

            Console.WriteLine("\n压测结束，按任意键退出...");
            Console.ReadKey();
        }

        static async Task SendRequest(HttpClient httpClient, string url, TestStats stats)
        {
            var requestStopwatch = Stopwatch.StartNew();
            try
            {
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                requestStopwatch.Stop();
                
                lock (stats)
                {
                    stats.SuccessCount++;
                    stats.TotalResponseTime += requestStopwatch.ElapsedMilliseconds;
                }
            }
            catch (Exception ex)
            {
                requestStopwatch.Stop();
                lock (stats)
                {
                    stats.FailedCount++;
                    Console.WriteLine($"请求失败: {ex.Message}");
                }
            }
        }
    }
}
