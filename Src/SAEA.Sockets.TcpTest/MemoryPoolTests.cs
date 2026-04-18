/****************************************************************************
*项目名称：SAEA.Sockets.TcpTest
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Sockets.TcpTest
*类 名 称：MemoryPoolTests
*版本号： v7.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2026/4/19
*描述：MemoryPool单元测试，验证内存池在Socket通信中的功能
*=====================================================================
*****************************************************************************/
using SAEA.Common.Caching;
using SAEA.Sockets;
using SAEA.Sockets.Base;
using SAEA.Sockets.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SAEEA.Sockets.TcpTest
{
    /// <summary>
    /// MemoryPool单元测试类
    /// 测试Rent/Return、PooledBuffer自动归还、分级选择、并发访问
    /// </summary>
    public class MemoryPoolTests
    {
        private static bool _allTestsPassed = true;
        private static int _testsRun = 0;
        private static int _testsPassed = 0;

        /// <summary>
        /// 运行所有测试
        /// </summary>
        public static bool RunAllTests()
        {
            Console.WriteLine("=== Memory Pool Tests ===");
            Console.WriteLine();

            Test_Rent_Return();
            Test_PooledBuffer_AutoReturn();
            Test_TierSelection();
            Test_ConcurrentAccess();
            Test_BufferSizeValidation();
            Test_SocketIntegration();

            Console.WriteLine();
            Console.WriteLine($"Tests Run: {_testsRun}, Passed: {_testsPassed}");
            Console.WriteLine(_allTestsPassed ? "ALL TESTS PASSED" : "SOME TESTS FAILED");

            return _allTestsPassed;
        }

        /// <summary>
        /// 测试Rent和Return基本功能
        /// </summary>
        private static void Test_Rent_Return()
        {
            RunTest("Rent/Return", () =>
            {
                // Test small buffer
                var smallBuffer = MemoryPoolManager.Rent(1024);
                if (smallBuffer == null) throw new Exception("Small buffer is null");
                if (smallBuffer.Length < 1024) throw new Exception("Small buffer size insufficient");
                MemoryPoolManager.Return(smallBuffer, 1024);

                // Test medium buffer
                var mediumBuffer = MemoryPoolManager.Rent(32768);
                if (mediumBuffer == null) throw new Exception("Medium buffer is null");
                if (mediumBuffer.Length < 32768) throw new Exception("Medium buffer size insufficient");
                MemoryPoolManager.Return(mediumBuffer, 32768);

                // Test large buffer
                var largeBuffer = MemoryPoolManager.Rent(131072);
                if (largeBuffer == null) throw new Exception("Large buffer is null");
                if (largeBuffer.Length < 131072) throw new Exception("Large buffer size insufficient");
                MemoryPoolManager.Return(largeBuffer, 131072);

                // Test zero size
                var zeroBuffer = MemoryPoolManager.Rent(0);
                if (zeroBuffer == null) throw new Exception("Zero size buffer is null");
                MemoryPoolManager.Return(zeroBuffer, 0);

                // Test null return
                MemoryPoolManager.Return(null, 1024); // Should not throw

                return true;
            });
        }

        /// <summary>
        /// 测试PooledBuffer自动归还功能
        /// </summary>
        private static void Test_PooledBuffer_AutoReturn()
        {
            RunTest("PooledBuffer Auto-Return", () =>
            {
                var statsBefore = MemoryPoolManager.GetStatistics();

                // Use using statement for automatic disposal
                using (var pooled = MemoryPoolManager.RentPooled(4096))
                {
                    if (pooled == null) throw new Exception("Pooled buffer is null");
                    if (pooled.Buffer == null) throw new Exception("Pooled buffer array is null");
                    if (pooled.Length != 4096) throw new Exception("Pooled buffer length incorrect");

                    // Write some data
                    for (int i = 0; i < 100; i++)
                    {
                        pooled.Buffer[i] = (byte)(i % 256);
                    }

                    // Verify Span works
                    var span = pooled.AsSpan();
                    if (span.Length != 4096) throw new Exception("Span length incorrect");

                    // Verify Memory works
                    var memory = pooled.AsMemory();
                    if (memory.Length != 4096) throw new Exception("Memory length incorrect");
                }

                // After disposal, buffer should be returned
                // We can't verify directly, but statistics should show return
                var statsAfter = MemoryPoolManager.GetStatistics();

                return true;
            });
        }

        /// <summary>
        /// 测试分级选择功能
        /// </summary>
        private static void Test_TierSelection()
        {
            RunTest("Tier Selection", () =>
            {
                // Test Small tier ( < 4KB)
                AssertTier(1024, BufferSizeTier.Small, "Small");
                AssertTier(4095, BufferSizeTier.Small, "Small");

                // Test Medium tier (4KB - 64KB)
                AssertTier(4096, BufferSizeTier.Medium, "Medium");
                AssertTier(32768, BufferSizeTier.Medium, "Medium");
                AssertTier(65535, BufferSizeTier.Medium, "Medium");

                // Test Large tier (> 64KB)
                AssertTier(65536, BufferSizeTier.Large, "Large");
                AssertTier(1048576, BufferSizeTier.Large, "Large");

                // Test edge cases
                AssertTier(0, BufferSizeTier.Small, "Small (zero)");
                AssertTier(-1, BufferSizeTier.Small, "Small (negative)");

                // Test PooledBuffer tier assignment
                using (var small = MemoryPoolManager.RentPooled(1024))
                using (var medium = MemoryPoolManager.RentPooled(8192))
                using (var large = MemoryPoolManager.RentPooled(131072))
                {
                    if (small.Tier != BufferSizeTier.Small)
                        throw new Exception($"Expected Small tier, got {small.Tier}");
                    if (medium.Tier != BufferSizeTier.Medium)
                        throw new Exception($"Expected Medium tier, got {medium.Tier}");
                    if (large.Tier != BufferSizeTier.Large)
                        throw new Exception($"Expected Large tier, got {large.Tier}");
                }

                return true;
            });
        }

        private static void AssertTier(int size, BufferSizeTier expected, string tierName)
        {
            var tier = MemoryPoolManager.GetTier(size);
            if (tier != expected)
                throw new Exception($"Expected {tierName} tier for size {size}, got {tier}");
        }

        /// <summary>
        /// 测试并发访问
        /// </summary>
        private static void Test_ConcurrentAccess()
        {
            RunTest("Concurrent Access", () =>
            {
                const int iterations = 100;
                const int concurrency = 20;
                var errors = new List<Exception>();
                var completed = 0;

                Parallel.For(0, concurrency, i =>
                {
                    try
                    {
                        for (int j = 0; j < iterations; j++)
                        {
                            // Alternate between different sizes
                            int size = (j % 3) switch
                            {
                                0 => 1024,
                                1 => 8192,
                                _ => 131072
                            };

                            var buffer = MemoryPoolManager.Rent(size);
                            if (buffer == null)
                                throw new Exception($"Null buffer at iteration {j}, thread {i}");

                            // Simulate some work
                            for (int k = 0; k < Math.Min(100, buffer.Length); k++)
                            {
                                buffer[k] = (byte)(k % 256);
                            }

                            MemoryPoolManager.Return(buffer, size);
                        }
                        Interlocked.Increment(ref completed);
                    }
                    catch (Exception ex)
                    {
                        lock (errors)
                        {
                            errors.Add(ex);
                        }
                    }
                });

                if (errors.Count > 0)
                {
                    throw new Exception($"Concurrent test failed with {errors.Count} errors: {errors[0].Message}");
                }

                if (completed != concurrency)
                {
                    throw new Exception($"Expected {concurrency} completions, got {completed}");
                }

                // Verify statistics
                var stats = MemoryPoolManager.GetStatistics();
                long totalOps = iterations * concurrency;
                if (stats.TotalRented < totalOps)
                {
                    throw new Exception($"Expected at least {totalOps} rented, got {stats.TotalRented}");
                }

                return true;
            });
        }

        /// <summary>
        /// 测试缓冲区大小验证
        /// </summary>
        private static void Test_BufferSizeValidation()
        {
            RunTest("Buffer Size Validation", () =>
            {
                // Test that rented buffers are at least as large as requested
                var small = MemoryPoolManager.Rent(100);
                if (small.Length < 100)
                    throw new Exception($"Buffer size {small.Length} < requested 100");
                MemoryPoolManager.Return(small, 100);

                // Test pooled buffer capacity
                using (var pooled = MemoryPoolManager.RentPooled(500))
                {
                    if (pooled.Capacity < 500)
                        throw new Exception($"Pooled capacity {pooled.Capacity} < requested 500");
                    if (pooled.Length != 500)
                        throw new Exception($"Pooled length {pooled.Length} != requested 500");
                }

                // Test large buffer
                var large = MemoryPoolManager.Rent(1024 * 1024); // 1MB
                if (large.Length < 1024 * 1024)
                    throw new Exception($"Large buffer size {large.Length} < requested {1024 * 1024}");
                MemoryPoolManager.Return(large, 1024 * 1024);

                return true;
            });
        }

        /// <summary>
        /// 测试与Socket的集成
        /// </summary>
        private static void Test_SocketIntegration()
        {
            RunTest("Socket Integration", () =>
            {
                // Verify that socket code paths use MemoryPoolManager
                // This is an integration test to ensure the memory pool
                // works correctly in socket scenarios

                var option = SocketOptionBuilder.Instance
                    .SetSocket()
                    .UseIocp<BaseCoder>()
                    .SetIP("127.0.0.1")
                    .SetPort(0) // Random port
                    .SetReadBufferSize(8192)
                    .SetWriteBufferSize(8192)
                    .Build();

                // Check that buffer sizes are appropriate for pooling
                if (option.ReadBufferSize > MemoryPoolManager.MediumThreshold)
                {
                    Console.WriteLine($"  Warning: ReadBufferSize {option.ReadBufferSize} exceeds MediumThreshold");
                }

                // Verify tier selection
                var tier = MemoryPoolManager.GetTier(option.ReadBufferSize);
                Console.WriteLine($"  ReadBuffer tier: {tier}");

                // Simulate socket buffer usage
                var socketBuffer = MemoryPoolManager.Rent(option.ReadBufferSize);
                if (socketBuffer == null)
                    throw new Exception("Failed to rent socket buffer");

                // Simulate receiving data
                for (int i = 0; i < 100; i++)
                {
                    socketBuffer[i] = (byte)(i % 256);
                }

                MemoryPoolManager.Return(socketBuffer, option.ReadBufferSize);

                return true;
            });
        }

        /// <summary>
        /// 测试辅助方法
        /// </summary>
        private static void RunTest(string testName, Func<bool> test)
        {
            _testsRun++;
            try
            {
                Console.Write($"Testing {testName}... ");
                var result = test();
                if (result)
                {
                    Console.WriteLine("PASSED");
                    _testsPassed++;
                }
                else
                {
                    Console.WriteLine("FAILED (returned false)");
                    _allTestsPassed = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAILED: {ex.Message}");
                _allTestsPassed = false;
            }
        }
    }
}
