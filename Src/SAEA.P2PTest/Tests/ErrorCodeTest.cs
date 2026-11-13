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
                (ErrorCode.DiscoveryNoResponse, "ED01"),
                (ErrorCode.NoCandidatePair, "EI01"),
                (ErrorCode.IceTcpSimultaneousOpenUnsupported, "EI04"),
                (ErrorCode.TurnUnavailable, "ET01"),
                (ErrorCode.TurnAuthFailed, "ET02"),
                (ErrorCode.ProtocolVersionMismatch, "ES01"),
                (ErrorCode.AuthFailed, "EA01")
            };

            bool allMatched = true;
            foreach (var (code, expectedCode) in errorCodes)
            {
                var description = ErrorCode.GetDescription(code);
                ConsoleHelper.WriteLine($"{expectedCode}: {code} - {description}");

                if (code != expectedCode || description == "Unknown error")
                {
                    allMatched = false;
                    ConsoleHelper.WriteLine($"  Mismatch: expected {expectedCode}, registered description missing or code differs");
                }
            }

            ConsoleHelper.WriteLine(allMatched ? "TestErrorCodeDefinitions: PASSED" : "TestErrorCodeDefinitions: FAILED");
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
                throw new P2PException(ErrorCode.NoCandidatePair, "Custom message");
            }
            catch (P2PException ex)
            {
                ConsoleHelper.WriteLine($"ErrorCode: {ex.ErrorCode}");
                ConsoleHelper.WriteLine($"Message: {ex.Message}");
                if (ex.ErrorCode == ErrorCode.NoCandidatePair && ex.Message == "Custom message")
                {
                    passed++;
                }
            }

            try
            {
                throw new P2PException(ErrorCode.TurnUnavailable, "With inner", new InvalidOperationException("Inner"));
            }
            catch (P2PException ex)
            {
                ConsoleHelper.WriteLine($"ErrorCode: {ex.ErrorCode}");
                ConsoleHelper.WriteLine($"Message: {ex.Message}");
                ConsoleHelper.WriteLine($"InnerException: {ex.InnerException?.Message}");
                if (ex.ErrorCode == ErrorCode.TurnUnavailable && 
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