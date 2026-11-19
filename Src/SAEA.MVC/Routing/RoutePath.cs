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
*命名空间：SAEA.MVC.Routing
*文件名： RoutePath
*版本号： v26.11.16.1
*唯一标识：f050ccc4-becf-44de-b031-04da751b6e19
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 14:27:41
*描述：请求路径的归一化、分段与解码
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 14:27:41
*修改人： yswenli
*版本号： v26.11.16.1
*描述：新增
*
*****************************************************************************/
using System.Text;

namespace SAEA.MVC.Routing
{
    /// <summary>
    /// 请求路径的归一化、分段与解码
    /// </summary>
    internal static class RoutePath
    {
        const char Slash = '/';

        /// <summary>
        /// 取 request-target 中 ? 之前的路径并归一化：单前导 /、合并连续 /、无尾斜杠
        /// </summary>
        /// <param name="relativeUrl">原始 request-target</param>
        /// <returns>归一化后的绝对路径</returns>
        internal static string NormalizePath(string relativeUrl)
        {
            if (string.IsNullOrEmpty(relativeUrl)) return "/";

            var end = relativeUrl.IndexOf('?');

            if (end < 0) end = relativeUrl.Length;

            // 常见路径已规范，只截掉 query，避免每个请求都重建字符串
            var canonical = end > 0 && relativeUrl[0] == Slash;

            if (canonical)
            {
                for (var i = 1; i < end; i++)
                {
                    if (relativeUrl[i] != Slash) continue;

                    if (relativeUrl[i - 1] == Slash || i == end - 1)
                    {
                        canonical = false;
                        break;
                    }
                }
            }

            if (canonical)
                return end == relativeUrl.Length ? relativeUrl : relativeUrl.Substring(0, end);

            return BuildNormalizedPath(relativeUrl, end);
        }

        private static string BuildNormalizedPath(string path, int end)
        {
            var builder = new StringBuilder(end + 1);

            builder.Append(Slash);

            // 前导 / 已写入，连续 / 直接跳过
            var lastWasSlash = true;

            for (var i = 0; i < end; i++)
            {
                var ch = path[i];

                if (ch == Slash)
                {
                    if (lastWasSlash) continue;

                    builder.Append(Slash);
                    lastWasSlash = true;
                }
                else
                {
                    builder.Append(ch);
                    lastWasSlash = false;
                }
            }

            if (builder.Length > 1 && builder[builder.Length - 1] == Slash) builder.Length--;

            return builder.ToString();
        }

        /// <summary>
        /// 按段解码含转义的路径并重建；任一段解码失败或解码出 / 时判为不匹配
        /// </summary>
        /// <param name="path">归一化路径</param>
        /// <param name="decoded">解码后的路径，段数保持不变</param>
        /// <returns>可继续匹配时返回 true</returns>
        internal static bool TryDecodePath(string path, out string decoded)
        {
            decoded = null;

            var builder = new StringBuilder(path.Length);

            builder.Append(Slash);

            var pos = path.Length > 0 && path[0] == Slash ? 1 : 0;

            while (true)
            {
                int start;
                int length;

                if (!TryReadSegment(path, ref pos, out start, out length)) break;

                string value;

                // 解码出 / 说明请求用 %2F 越段，按不匹配处理，不能当成两个段
                if (!RouteSegmentMatcher.TryDecodeSegment(path, start, length, out value)) return false;

                if (builder.Length > 1) builder.Append(Slash);

                builder.Append(value);
            }

            decoded = builder.ToString();

            return true;
        }

        internal static int CountSegments(string path)
        {
            var count = 0;

            var last = path.Length - 1;

            for (var i = 0; i < last; i++)
            {
                if (path[i] == Slash) count++;
            }

            return count;
        }

        /// <summary>
        /// 从 pos 处读出一个段的边界；归一化路径里不存在空段
        /// </summary>
        /// <param name="path">归一化路径</param>
        /// <param name="pos">读取位置，返回时指向下一段的起点</param>
        /// <param name="start">段首索引</param>
        /// <param name="length">段长度</param>
        /// <returns>读到段返回 true，已到末尾返回 false</returns>
        internal static bool TryReadSegment(string path, ref int pos, out int start, out int length)
        {
            start = pos;

            length = 0;

            if (pos >= path.Length) return false;

            var index = path.IndexOf(Slash, pos);

            if (index < 0)
            {
                length = path.Length - pos;
                pos = path.Length;
            }
            else
            {
                length = index - pos;
                pos = index + 1;
            }

            return length > 0;
        }
    }
}