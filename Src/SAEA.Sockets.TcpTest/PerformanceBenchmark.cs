/****************************************************************************
*项目名称：SAEA.Sockets.TcpTest
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Sockets.TcpTest
*类 名 称：PerformanceBenchmark
*版本号： v7.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2026/4/19
*描述：MemoryPool性能基准测试，对比池化与非池化性能
*=====================================================================
*****************************************************************************/
using SAEA.Common.Caching;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SAEEA.Sockets.TcpTest
{
    /// <summary>
    /// 内存池性能基准测试类
    /// 测试池化 vs 非池化的性能差异
    /// </summary>
    public class PerformanceBenchmark
    {
        private const int WarmupIterations = 1000;
        private const int TestIterations = 10000;
        private const int BufferSize = 8192; // Typical socket buffer size

        /// <summary>
        /// 运行所有基准测试
        /// </summary>
        public static void RunAllBenchmarks()
        {
            Console.WriteLine("=== Memory Pool Performance Benchmarks ===");
            Console.WriteLine($"Test Iterations: {TestIterations:N0}");
            Console.WriteLine($"Buffer Size: {BufferSize:N0} bytes");
            Console.WriteLine();

            // Warmup
            Console.WriteLine("Warming up...");
            RunWarmup();

            // Run benchmarks
            Benchmark_RentReturn();
            Benchmark_PooledBuffer();
            Benchmark_ConcurrentAccess();
            Benchmark_GCImpact();

            Console.WriteLine();
            Console.WriteLine("=== Benchmark Complete ===");
        }

        /// <summary>
        /// 预热
        /// </summary>
        private static void RunWarmup()
        {
            for (int i = 0; i < WarmupIterations; i++)
            {
                var buffer = MemoryPoolManager.Rent(BufferSize);
                MemoryPoolManager.Return(buffer, BufferSize);
            }
        }

        /// <summary>
        /// 测试Rent/Return性能
        /// </summary>
        private static void Benchmark_RentReturn()
        {
            Console.WriteLine("--- Rent/Return Benchmark ---");

            // Benchmark pooled
            var pooledTime = BenchmarkOperation("Pooled", () =>
            {
                var buffer = MemoryPoolManager.Rent(BufferSize);
                // Simulate some work
                buffer[0] = 1;
                buffer[BufferSize - 1] = 2;
                MemoryPoolManager.Return(buffer, BufferSize);
            }, TestIterations);

            // Benchmark non-pooled (new byte[])
            var nonPooledTime = BenchmarkOperation("Non-Pooled", () =>
            {
                var buffer = new byte[BufferSize];
                // Simulate some work
                buffer[0] = 1;
                buffer[BufferSize - 1] = 2;
                // Buffer goes to GC
            }, TestIterations);

            // Benchmark ArrayPool.Shared directly
            var arrayPoolTime = BenchmarkOperation("ArrayPool.Shared", () =>
            {
                var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
                // Simulate some work
                buffer[0] = 1;
                buffer[BufferSize - 1] = 2;
                ArrayPool<byte>.Shared.Return(buffer);
            }, TestIterations);

            // Report results
            ReportResults("Rent/Return", pooledTime, nonPooledTime, arrayPoolTime);
        }

        /// <summary>
        /// 测试PooledBuffer性能
        /// </summary>
        private static void Benchmark_PooledBuffer()
        {
            Console.WriteLine("--- PooledBuffer Benchmark ---");

            // Benchmark using PooledBuffer
            var pooledTime = BenchmarkOperation("PooledBuffer", () =>
            {
                using (var pooled = MemoryPoolManager.RentPooled(BufferSize))
                {
                    // Simulate some work
                    pooled.Buffer[0] = 1;
                    pooled.Buffer[BufferSize - 1] = 2;
                    var span = pooled.AsSpan();
                    var memory = pooled.AsMemory();
                }
            }, TestIterations);

            // Benchmark manual Rent/Return
            var manualTime = BenchmarkOperation("Manual Rent/Return", () =>
            {
                var buffer = MemoryPoolManager.Rent(BufferSize);
                try
                {
                    // Simulate some work
                    buffer[0] = 1;
                    buffer[BufferSize - 1] = 2;
                }
                finally
                {
                    MemoryPoolManager.Return(buffer, BufferSize);
                }
            }, TestIterations);

            // Benchmark non-pooled
            var nonPooledTime = BenchmarkOperation("Non-Pooled", () =>
            {
                var buffer = new byte[BufferSize];
                // Simulate some work
                buffer[0] = 1;
                buffer[BufferSize - 1] = 2;
            }, TestIterations);

            ReportResults("PooledBuffer", pooledTime, nonPooledTime, manualTime);
        }

        /// <summary>
        /// 测试并发访问性能
        /// </summary>
        private static void Benchmark_ConcurrentAccess()
        {
            Console.WriteLine("--- Concurrent Access Benchmark ---");
            const int concurrency = 10;
            const int iterationsPerThread = TestIterations / concurrency;

            // Benchmark pooled concurrent
            var pooledTime = BenchmarkOperationConcurrent("Pooled", () =>
            {
                var buffer = MemoryPoolManager.Rent(BufferSize);
                buffer[0] = 1;
                buffer[BufferSize - 1] = 2;
                MemoryPoolManager.Return(buffer, BufferSize);
            }, iterationsPerThread, concurrency);

            // Benchmark non-pooled concurrent
            var nonPooledTime = BenchmarkOperationConcurrent("Non-Pooled", () =>
            {
                var buffer = new byte[BufferSize];
                buffer[0] = 1;
                buffer[BufferSize - 1] = 2;
            }, iterationsPerThread, concurrency);

            // Benchmark ArrayPool concurrent
            var arrayPoolTime = BenchmarkOperationConcurrent("ArrayPool.Shared", () =>
            {
                var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
                buffer[0] = 1;
                buffer[BufferSize - 1] = 2;
                ArrayPool<byte>.Shared.Return(buffer);
            }, iterationsPerThread, concurrency);

            ReportResults("Concurrent Access", pooledTime, nonPooledTime, arrayPoolTime);
        }

        /// <summary>
        /// 测试GC影响
        /// </summary>
        private static void Benchmark_GCImpact()
        {
            Console.WriteLine("--- GC Impact Benchmark ---");

            const int gcTestIterations = 100000;
            const int gcTestBufferSize = 4096;

            // Force GC before tests
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long gen0Before = GC.CollectionCount(0);
            long gen1Before = GC.CollectionCount(1);
            long gen2Before = GC.CollectionCount(2);

            // Pooled test
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < gcTestIterations; i++)
            {
                var buffer = MemoryPoolManager.Rent(gcTestBufferSize);
                buffer[0] = (byte)(i % 256);
                MemoryPoolManager.Return(buffer, gcTestBufferSize);
            }
            sw.Stop();
            var pooledTime = sw.ElapsedMilliseconds;

            long gen0Pooled = GC.CollectionCount(0) - gen0Before;
            long gen1Pooled = GC.CollectionCount(1) - gen1Before;
            long gen2Pooled = GC.CollectionCount(2) - gen2Before;

            // Force GC
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            gen0Before = GC.CollectionCount(0);
            gen1Before = GC.CollectionCount(1);
            gen2Before = GC.CollectionCount(2);

            // Non-pooled test
            sw.Restart();
            for (int i = 0; i < gcTestIterations; i++)
            {
                var buffer = new byte[gcTestBufferSize];
                buffer[0] = (byte)(i % 256);
            }
            sw.Stop();
            var nonPooledTime = sw.ElapsedMilliseconds;

            long gen0NonPooled = GC.CollectionCount(0) - gen0Before;
            long gen1NonPooled = GC.CollectionCount(1) - gen1Before;
            long gen2NonPooled = GC.CollectionCount(2) - gen2Before;

            // Force GC and collect final stats
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Console.WriteLine($"Pooled:");
            Console.WriteLine($"  Time: {pooledTime:N0} ms");
            Console.WriteLine($"  Gen0: {gen0Pooled}, Gen1: {gen1Pooled}, Gen2: {gen2Pooled}");

            Console.WriteLine($"Non-Pooled:");
            Console.WriteLine($"  Time: {nonPooledTime:N0} ms");
            Console.WriteLine($"  Gen0: {gen0NonPooled}, Gen1: {gen1NonPooled}, Gen2: {gen2NonPooled}");

            if (pooledTime > 0)
            {
                var speedup = (double)nonPooledTime / pooledTime;
                Console.WriteLine($"Speedup: {speedup:F2}x");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// 执行单线程基准测试
        /// </summary>
        private static long BenchmarkOperation(string name, Action operation, int iterations)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                operation();
            }
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }

        /// <summary>
        /// 执行并发基准测试
        /// </summary>
        private static long BenchmarkOperationConcurrent(string name, Action operation, int iterationsPerThread, int concurrency)
        {
            var sw = Stopwatch.StartNew();
            Parallel.For(0, concurrency, _ =>
            {
                for (int i = 0; i < iterationsPerThread; i++)
                {
                    operation();
                }
            });
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }

        /// <summary>
        /// 报告结果
        /// </summary>
        private static void ReportResults(string testName, long pooledTime, long nonPooledTime, long referenceTime = 0)
        {
            Console.WriteLine($"Results for {testName}:");
            Console.WriteLine($"  Pooled:     {pooledTime:N0} ms");
            Console.WriteLine($"  Non-Pooled: {nonPooledTime:N0} ms");
            if (referenceTime > 0)
            {
                Console.WriteLine($"  Reference:  {referenceTime:N0} ms");
            }

            if (pooledTime > 0 && nonPooledTime > 0)
            {
                double speedup = (double)nonPooledTime / pooledTime;
                double improvement = ((nonPooledTime - pooledTime) / (double)nonPooledTime) * 100;
                Console.WriteLine($"  Speedup: {speedup:F2}x ({improvement:F1}% faster)");

                // Calculate operations per second
                double pooledOps = TestIterations / (pooledTime / 1000.0);
                double nonPooledOps = TestIterations / (nonPooledTime / 1000.0);
                Console.WriteLine($"  Pooled OPS:     {pooledOps:N0}");
                Console.WriteLine($"  Non-Pooled OPS: {nonPooledOps:N0}");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// 生成性能报告摘要
        /// </summary>
        public static void GenerateReport()
        {
            Console.WriteLine();
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           Memory Pool Performance Report                       ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║  Small Threshold:  {MemoryPoolManager.SmallThreshold:N0} bytes                            ║");
            Console.WriteLine($"║  Medium Threshold: {MemoryPoolManager.MediumThreshold:N0} bytes                           ║");
            Console.WriteLine($"║  Large Threshold:  {MemoryPoolManager.LargeThreshold:N0} bytes                          ║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");

            var stats = MemoryPoolManager.GetStatistics();
            Console.WriteLine($"║  Total Rented:  {stats.TotalRented:N0}".PadRight(65) + "║");
            Console.WriteLine($"║  Total Returned: {stats.TotalReturned:N0}".PadRight(65) + "║");
            Console.WriteLine($"║  Active Buffers: {stats.ActiveBuffers:N0}".PadRight(65) + "║");
            Console.WriteLine("╠════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║  Key Benefits:                                                 ║");
            Console.WriteLine("║  • Reduced GC pressure by reusing buffers                      ║");
            Console.WriteLine("║  • Better memory locality and cache utilization                ║");
            Console.WriteLine("║  • Lower allocation overhead in high-throughput scenarios      ║");
            Console.WriteLine("║  • Tiered pools optimize for different buffer sizes            ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// 性能基准测试结果
    /// </summary>
    public class BenchmarkResult
    {
        public string TestName { get; set; }
        public long PooledTimeMs { get; set; }
        public long NonPooledTimeMs { get; set; }
        public long ReferenceTimeMs { get; set; }
        public double Speedup { get; set; }
        public double PooledOpsPerSecond { get; set; }
        public double NonPooledOpsPerSecond { get; set; }
    }
}
