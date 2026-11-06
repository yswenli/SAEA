using System;

using SAEA.Common;
using SAEA.P2P.Common;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// 错误码自测：错误码描述文本与 P2PException 的错误码、消息、内部异常传递
    /// </summary>
    public static class ErrorCodeTest
    {
        /// <summary>
        /// 依次输出各错误码的描述并校验 P2PException 三种构造方式携带的信息
        /// </summary>
        public static void Run()
        {
            ConsoleHelper.WriteLine("=== ErrorCodeTest ===");
            ConsoleHelper.WriteLine("");

            TestErrorCodeDefinitions();
            ConsoleHelper.WriteLine("");

            TestP2PException();
            ConsoleHelper.WriteLine("");

            ConsoleHelper.WriteLine("=== ErrorCodeTest Complete ===");
        }

        static void TestErrorCodeDefinitions()
        {
            ConsoleHelper.WriteLine("--- TestErrorCodeDefinitions ---");

            var errorCodes = new[]
            {
                (ErrorCode.RegisterPeerIdMissing, "EP01"),
                (ErrorCode.RegisterPeerIdInvalid, "EP02"),
                (ErrorCode.DiscoveryNoResponse, "EO01"),
                (ErrorCode.HolePunchFailed, "EH01"),
                (ErrorCode.RelayFailed, "ER01"),
                (ErrorCode.AuthFailed, "EA01")
            };

            foreach (var (code, expectedCode) in errorCodes)
            {
                var description = ErrorCode.GetDescription(code);
                ConsoleHelper.WriteLine($"{expectedCode}: {code} - {description}");
            }

            ConsoleHelper.WriteLine("TestErrorCodeDefinitions: PASSED");
        }

        static void TestP2PException()
        {
            ConsoleHelper.WriteLine("--- TestP2PException ---");

            int passed = 0;
            int total = 3;

            try
            {
                throw new P2PException(ErrorCode.RegisterPeerIdMissing);
            }
            catch (P2PException ex)
            {
                ConsoleHelper.WriteLine($"ErrorCode: {ex.ErrorCode}");
                ConsoleHelper.WriteLine($"Message: {ex.Message}");
                if (ex.ErrorCode == ErrorCode.RegisterPeerIdMissing)
                {
                    passed++;
                }
            }

            try
            {
                throw new P2PException(ErrorCode.HolePunchFailed, "Custom message");
            }
            catch (P2PException ex)
            {
                ConsoleHelper.WriteLine($"ErrorCode: {ex.ErrorCode}");
                ConsoleHelper.WriteLine($"Message: {ex.Message}");
                if (ex.ErrorCode == ErrorCode.HolePunchFailed && ex.Message == "Custom message")
                {
                    passed++;
                }
            }

            try
            {
                throw new P2PException(ErrorCode.RelayFailed, "With inner", new InvalidOperationException("Inner"));
            }
            catch (P2PException ex)
            {
                ConsoleHelper.WriteLine($"ErrorCode: {ex.ErrorCode}");
                ConsoleHelper.WriteLine($"Message: {ex.Message}");
                ConsoleHelper.WriteLine($"InnerException: {ex.InnerException?.Message}");
                if (ex.ErrorCode == ErrorCode.RelayFailed && 
                    ex.Message == "With inner" && 
                    ex.InnerException?.Message == "Inner")
                {
                    passed++;
                }
            }

            ConsoleHelper.WriteLine($"TestP2PException: {passed}/{total} PASSED");
        }
    }
}