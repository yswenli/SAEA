using System;
using SAEA.Common;

namespace SAEA.P2P.Common
{
    internal static class P2PLogHelper
    {
        private static int _logLevel = 2;

        public static void SetLevel(int level)
        {
            _logLevel = level;
        }

        public static void Trace(string nodeId, string message)
        {
            if (_logLevel <= 0)
            {
                LogHelper.Debug($"[P2P][Trace][{nodeId}] {message}");
            }
        }

        public static void Debug(string nodeId, string message)
        {
            if (_logLevel <= 1)
            {
                LogHelper.Debug($"[P2P][Debug][{nodeId}] {message}");
            }
        }

        public static void Info(string nodeId, string message)
        {
            if (_logLevel <= 2)
            {
                LogHelper.Info($"[P2P][Info][{nodeId}] {message}");
            }
        }

        public static void Warning(string nodeId, string message)
        {
            if (_logLevel <= 3)
            {
                LogHelper.Info($"[P2P][Warning][{nodeId}] {message}");
            }
        }

        public static void Error(string nodeId, string errorCode, string message)
        {
            LogHelper.Info($"[P2P][Error][{nodeId}][{errorCode}] {message}");
        }

        public static void Error(string nodeId, string errorCode, string message, Exception ex)
        {
            LogHelper.Error($"[P2P][Error][{nodeId}][{errorCode}] {message}", ex);
        }
    }
}