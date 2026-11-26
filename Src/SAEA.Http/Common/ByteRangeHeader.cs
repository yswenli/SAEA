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
*命名空间：SAEA.Http.Common
*文件名： ByteRangeHeader
*版本号： v26.11.25.1
*唯一标识：a32cdedb-3208-4289-bdf9-11a23a002912
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/26 10:00:00
*描述：单区间 Range 请求头解析
*
*=====================================================================
*修改标记
*修改时间：2026/11/26 10:00:00
*修改人： yswenli
*版本号： v26.11.25.1
*描述：新增，只解析单区间 Range 并按 RFC 7233 区分忽略、可满足与不可满足
*
*****************************************************************************/
using System;
using System.Globalization;

namespace SAEA.Http.Common
{
    /// <summary>
    /// Range 头解析结果的分类
    /// </summary>
    internal enum ByteRangeParseKind
    {
        /// <summary>
        /// 语法非法、多区间或缺少头，按 RFC 7233 忽略 Range
        /// </summary>
        Ignore,

        /// <summary>
        /// 区间落在正文范围内，可按单区间返回 206
        /// </summary>
        Satisfiable,

        /// <summary>
        /// 语法合法但区间超出正文范围，应返回 416
        /// </summary>
        Unsatisfiable
    }

    /// <summary>
    /// 单区间 Range 头的解析结果，Start 与 End 为闭区间
    /// </summary>
    internal struct ByteRangeParseResult
    {
        /// <summary>
        /// 解析分类
        /// </summary>
        public ByteRangeParseKind Kind { get; }

        /// <summary>
        /// 区间起始字节下标，仅 Satisfiable 时有效
        /// </summary>
        public long Start { get; }

        /// <summary>
        /// 区间结束字节下标（含），仅 Satisfiable 时有效
        /// </summary>
        public long End { get; }

        /// <summary>
        /// 区间字节数，仅 Satisfiable 时非 0
        /// </summary>
        public long Length { get; }

        /// <summary>
        /// 按指定分类与闭区间构造结果
        /// </summary>
        /// <param name="kind">解析分类</param>
        /// <param name="start">区间起始下标</param>
        /// <param name="end">区间结束下标（含）</param>
        internal ByteRangeParseResult(ByteRangeParseKind kind, long start, long end)
        {
            Kind = kind;
            Start = start;
            End = end;
            Length = kind == ByteRangeParseKind.Satisfiable ? end - start + 1 : 0;
        }
    }

    /// <summary>
    /// 单区间 Range 头解析：只支持 bytes=a-b、bytes=a- 与 bytes=-N
    /// </summary>
    internal static class ByteRangeHeader
    {
        /// <summary>
        /// 解析 Range 请求头
        /// </summary>
        /// <param name="rangeHeader">Range 头原始值，可为 null</param>
        /// <param name="totalLength">完整正文长度</param>
        /// <returns>解析结果；语法非法或存在多区间时返回 Ignore</returns>
        internal static ByteRangeParseResult Parse(string rangeHeader, long totalLength)
        {
            // 缺少头或长度未知时按没有 Range 处理
            if (string.IsNullOrWhiteSpace(rangeHeader)) return Ignore();

            if (totalLength < 0) return Ignore();

            var header = rangeHeader.Trim();

            var equalIndex = header.IndexOf('=');

            if (equalIndex < 0) return Ignore();

            // unit 允许两侧有空白，但必须是 bytes（忽略大小写）
            if (!header.Substring(0, equalIndex).Trim().Equals("bytes", StringComparison.OrdinalIgnoreCase)) return Ignore();

            var spec = header.Substring(equalIndex + 1).Trim();

            if (spec.Length == 0) return Ignore();

            // 多区间不在本实现的范围内，按 RFC 7233 允许的方式忽略 Range
            if (spec.IndexOf(',') >= 0) return Ignore();

            var dashIndex = spec.IndexOf('-');

            // 连字符必须恰好一个，否则不是合法的单区间写法
            if (dashIndex < 0 || dashIndex != spec.LastIndexOf('-')) return Ignore();

            var firstPart = spec.Substring(0, dashIndex);

            var lastPart = spec.Substring(dashIndex + 1);

            // 两侧都为空没有可解析的区间
            if (firstPart.Length == 0 && lastPart.Length == 0) return Ignore();

            if (firstPart.Length == 0)
            {
                if (!TryParseNumber(lastPart, out var suffix)) return Ignore();

                // 0 字节文件没有可满足的区间；先判长度，避免写出 0--1 这种非法区间
                if (totalLength == 0) return Unsatisfiable();

                if (suffix == 0) return Unsatisfiable();

                // 后缀长度超过文件长度时返回整个文件
                if (suffix >= totalLength) return Satisfiable(0, totalLength - 1);

                return Satisfiable(totalLength - suffix, totalLength - 1);
            }

            if (!TryParseNumber(firstPart, out var start)) return Ignore();

            long end;

            if (lastPart.Length == 0)
            {
                if (totalLength == 0) return Unsatisfiable();

                if (start >= totalLength) return Unsatisfiable();

                end = totalLength - 1;
            }
            else
            {
                if (!TryParseNumber(lastPart, out var last)) return Ignore();

                // 末字节小于首字节属于非法区间，按忽略处理
                if (last < start) return Ignore();

                if (totalLength == 0) return Unsatisfiable();

                if (start >= totalLength) return Unsatisfiable();

                end = Math.Min(last, totalLength - 1);
            }

            return Satisfiable(start, end);
        }

        /// <summary>
        /// 按纯十进制无符号整数解析，拒绝正负号、空白与溢出
        /// </summary>
        /// <param name="text">待解析文本</param>
        /// <param name="value">解析结果</param>
        /// <returns>解析成功返回 true</returns>
        private static bool TryParseNumber(string text, out long value)
        {
            return long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// 构造忽略结果
        /// </summary>
        /// <returns>Ignore 结果</returns>
        private static ByteRangeParseResult Ignore()
        {
            return new ByteRangeParseResult(ByteRangeParseKind.Ignore, 0, 0);
        }

        /// <summary>
        /// 构造不可满足结果
        /// </summary>
        /// <returns>Unsatisfiable 结果</returns>
        private static ByteRangeParseResult Unsatisfiable()
        {
            return new ByteRangeParseResult(ByteRangeParseKind.Unsatisfiable, 0, 0);
        }

        /// <summary>
        /// 构造可满足结果
        /// </summary>
        /// <param name="start">区间起始下标</param>
        /// <param name="end">区间结束下标（含）</param>
        /// <returns>Satisfiable 结果</returns>
        private static ByteRangeParseResult Satisfiable(long start, long end)
        {
            return new ByteRangeParseResult(ByteRangeParseKind.Satisfiable, start, end);
        }
    }
}