/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.Http.Base
*文件名： HttpBodyStorage
*版本号： v26.11.24.1
*唯一标识：b41e6d7c-2a3f-4c9e-8b15-7d9a0c3e5f28
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/24 15:41:00
*描述：把超过阈值的请求体写入临时文件并返回文件载体流
*
*=====================================================================
*修改标记
*修改时间：2026/11/24 15:59:54
*修改人： yswenli
*版本号： v26.11.24.1
*描述：新增可供流式写入复用的临时文件创建与删除方法
*
*****************************************************************************/
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SAEA.Http.Base
{
    /// <summary>
    /// 请求体临时文件存储：按连接标识与随机串命名，失败时清理半成品再抛出。
    /// </summary>
    internal static class HttpBodyStorage
    {
        const string DefaultDirectoryName = "saea-http";

        const int MaxChannelIdLength = 64;

        /// <summary>
        /// 把请求体写入临时文件并返回文件载体的请求体流
        /// </summary>
        /// <param name="body">请求体字节</param>
        /// <param name="tempDirectory">临时目录，null 或空白时用系统临时目录下的 saea-http</param>
        /// <param name="channelId">连接标识，仅用于文件名前缀，会做安全化处理</param>
        /// <returns>文件载体的请求体流</returns>
        internal static HttpRequestBodyStream CreateFileBacked(ReadOnlyMemory<byte> body, string tempDirectory, string channelId)
        {
            string filePath = null;

            FileStream fileStream = null;

            try
            {
                // CreateNew 保证不会覆盖已有文件；随机串已足够避免重名
                fileStream = CreateTempFile(tempDirectory, channelId, out filePath);

                if (MemoryMarshal.TryGetArray(body, out var segment) && segment.Array != null)
                {
                    fileStream.Write(segment.Array, segment.Offset, segment.Count);
                }
                else
                {
                    var bytes = body.ToArray();

                    fileStream.Write(bytes, 0, bytes.Length);
                }

                fileStream.Position = 0;

                var result = HttpRequestBodyStream.FromFile(fileStream, filePath);

                fileStream = null;

                return result;
            }
            catch
            {
                if (fileStream != null)
                {
                    try
                    {
                        fileStream.Dispose();
                    }
                    catch
                    {
                        // 原始异常更重要，关闭失败的次生异常直接忽略
                    }
                }

                TryDelete(filePath);

                throw;
            }
        }

        /// <summary>
        /// 按统一命名创建可写的临时文件，供整段落盘与流式分段落盘共用
        /// </summary>
        /// <param name="tempDirectory">临时目录，null 或空白时用系统临时目录下的 saea-http</param>
        /// <param name="channelId">连接标识，仅用于文件名前缀，会做安全化处理</param>
        /// <param name="filePath">创建成功时返回临时文件路径</param>
        /// <returns>已打开的临时文件流</returns>
        internal static FileStream CreateTempFile(string tempDirectory, string channelId, out string filePath)
        {
            if (string.IsNullOrEmpty(tempDirectory))
                tempDirectory = Path.Combine(Path.GetTempPath(), DefaultDirectoryName);

            // CreateDirectory 幂等：目录已存在时不会抛异常
            Directory.CreateDirectory(tempDirectory);

            filePath = Path.Combine(tempDirectory, BuildFileName(channelId));

            try
            {
                return new FileStream(filePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
            }
            catch
            {
                // 打开失败时清掉可能的半成品，再把原始异常抛给调用方
                TryDelete(filePath);

                throw;
            }
        }

        static string BuildFileName(string channelId)
        {
            return "http-body-" + SanitizeChannelId(channelId) + "-" + Guid.NewGuid().ToString("N") + ".tmp";
        }

        static string SanitizeChannelId(string channelId)
        {
            if (string.IsNullOrEmpty(channelId)) return "conn";

            if (channelId.Length > MaxChannelIdLength)
                channelId = channelId.Substring(0, MaxChannelIdLength);

            var chars = channelId.ToCharArray();

            for (var i = 0; i < chars.Length; i++)
            {
                var c = chars[i];

                // 只保留可安全出现在文件名里的字符，其余统一替换，防止连接标识带路径分隔符
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_' && c != '.')
                    chars[i] = '_';
            }

            return new string(chars);
        }

        internal static void TryDelete(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            try
            {
                if (File.Exists(filePath)) File.Delete(filePath);
            }
            catch
            {
                // 清理半成品失败不影响抛出原始异常，残留文件留给系统临时目录回收
            }
        }
    }
}