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
*文件名： RouteSegmentMatcher
*版本号： v26.11.16.1
*唯一标识：adcb3fd3-3256-40d8-b7a8-13d585582d69
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 14:27:41
*描述：路径段匹配与 405 的 Allow 汇总
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 16:15:31
*修改人： yswenli
*版本号： v26.11.16.1
*描述：CollectAllow 把未声明动词的条目按 AllowOrder 全部声明处理，使 OPTIONS 自动应答不缺 Allow
*
*****************************************************************************/
using SAEA.Common;

using SAEA.MVC.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.MVC.Routing
{
    /// <summary>
    /// 路径段匹配与 405 的 Allow 汇总
    /// </summary>
    internal static class RouteSegmentMatcher
    {
        const char Slash = '/';

        private static bool SegmentEquals(string path, int start, int length, string literal)
        {
            return length == literal.Length && string.Compare(path, start, literal, 0, literal.Length, StringComparison.OrdinalIgnoreCase) == 0;
        }

        internal static bool TryMatchSegments(AttributeRouteEntry entry, string path, bool alreadyDecoded, out List<KeyValuePair<string, string>> values)
        {
            values = null;

            var segments = entry.Segments;

            var pos = path.Length > 0 && path[0] == Slash ? 1 : 0;

            List<KeyValuePair<string, string>> collected = null;

            for (var i = 0; i < segments.Length; i++)
            {
                int start;
                int length;

                if (!RoutePath.TryReadSegment(path, ref pos, out start, out length)) return false;

                var segment = segments[i];

                if (segment.IsParameter)
                {
                    string value;

                    // 路径已整体解码过：直接取原文，%25 这类内容不能被二次解码
                    if (alreadyDecoded)
                        value = path.Substring(start, length);
                    else if (!TryDecodeSegment(path, start, length, out value))
                        return false;

                    if (collected == null) collected = new List<KeyValuePair<string, string>>();

                    collected.Add(new KeyValuePair<string, string>(segment.Value, value));
                }
                else if (!SegmentEquals(path, start, length, segment.Value))
                {
                    return false;
                }
            }

            // 段数必须完全一致，多出来的段判为不匹配
            int restStart;
            int restLength;

            if (RoutePath.TryReadSegment(path, ref pos, out restStart, out restLength)) return false;

            values = collected;

            return true;
        }

        /// <summary>
        /// 解码单个路径段；解码后含 / 说明请求用 %2F 越段，判为不匹配
        /// </summary>
        /// <param name="path">归一化路径</param>
        /// <param name="start">段首索引</param>
        /// <param name="length">段长度</param>
        /// <param name="value">解码后的段值</param>
        /// <returns>可作为单个路径段使用时返回 true</returns>
        internal static bool TryDecodeSegment(string path, int start, int length, out string value)
        {
            var raw = path.Substring(start, length);

            if (raw.IndexOf('%') < 0)
            {
                value = raw;
            }
            else
            {
                try
                {
                    value = Uri.UnescapeDataString(raw);
                }
                catch (UriFormatException)
                {
                    // 非法转义不能让坏请求打断匹配，按原文继续比较
                    value = raw;
                }
                catch (ArgumentException)
                {
                    value = raw;
                }
            }

            return value.IndexOf(Slash) < 0;
        }

        internal static string CollectAllow(List<AttributeRouteEntry> entries)
        {
            var builder = new StringBuilder();

            var declaresOptions = false;

            for (var v = 0; v < HttpMethodCatalog.AllowOrder.Length; v++)
            {
                var verb = HttpMethodCatalog.AllowOrder[v];

                var declared = false;

                for (var i = 0; i < entries.Count; i++)
                {
                    // 未声明动词（HttpMethods 为 null）等价于对 AllowOrder 里的全部动词放行，
                    // 否则只有 [Route] 的路径在 OPTIONS 自动应答里拿不到 Allow
                    if (entries[i].HttpMethods == null || entries[i].DeclaresVerb(verb))
                    {
                        declared = true;

                        break;
                    }
                }

                // 声明 GET 即隐含 HEAD：HEAD 复用 GET 的执行结果，Allow 里也要同步给出
                if (!declared && HttpMethodCatalog.IsHead(verb))
                {
                    for (var i = 0; i < entries.Count; i++)
                    {
                        if (!entries[i].DeclaresVerb(ConstHelper.GET)) continue;

                        declared = true;

                        break;
                    }
                }

                if (!declared) continue;

                if (builder.Length > 0) builder.Append(", ");

                builder.Append(verb);
            }

            // 未声明动词的条目只是放行全部 AllowOrder，并不等于声明 OPTIONS，这里只认显式声明
            for (var i = 0; i < entries.Count; i++)
            {
                if (!entries[i].DeclaresVerb(ConstHelper.OPTIONS)) continue;

                declaresOptions = true;

                break;
            }

            return HttpMethodCatalog.WithExplicitOptions(builder.Length == 0 ? null : builder.ToString(), declaresOptions);
        }
    }
}