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
*命名空间：SAEA.P2P.Common
*文件名： P2PLogHelper
*版本号： v26.4.23.1
*唯一标识：4a9d4db0-c95d-4ee8-8e10-76a3de28d73f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/4/20 15:49:38
*描述：P2P日志帮助类，提供分级日志输出功能
*
*=====================================================================
*修改标记
*修改时间：2026/4/20 15:49:38
*修改人： yswenli
*版本号： v26.4.23.1
*描述：P2P日志帮助类，提供分级日志输出功能
*
*****************************************************************************/
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