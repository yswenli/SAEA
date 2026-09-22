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
 *文件名： P2PContent
 *版本号： v26.4.23.1
 *唯一标识：7c2b6f0a-5d34-4a91-8e6f-1b9c3a4d2e50
 *当前的用户域：WENLI-PC
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2026/09/22 11:00:00
 *描述：P2PContent帮助类
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/09/22 11:00:00
 *修改人： yswenli
 *版本号： v26.4.23.1
 *描述：P2PContent帮助类
 *
 *****************************************************************************/
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace SAEA.P2P.Common
{
    /// <summary>
    /// 帧体 UTF8 解码辅助：优先复用帧批次背衬数组，避免逐字段整块复制。
    /// </summary>
    internal static class P2PContent
    {
        public static string GetString(ReadOnlyMemory<byte> content)
        {
            if (content.IsEmpty) return string.Empty;
            if (MemoryMarshal.TryGetArray(content, out var segment) && segment.Array != null)
                return Encoding.UTF8.GetString(segment.Array, segment.Offset, segment.Count);
            return Encoding.UTF8.GetString(content.ToArray());
        }

        public static string GetString(ReadOnlyMemory<byte> content, int start, int length)
        {
            if (length <= 0) return string.Empty;
            if (MemoryMarshal.TryGetArray(content, out var segment) && segment.Array != null)
                return Encoding.UTF8.GetString(segment.Array, segment.Offset + start, length);
            return Encoding.UTF8.GetString(content.Span.Slice(start, length).ToArray());
        }

        public static int IndexOf(ReadOnlySpan<byte> content, byte value, int start)
        {
            var relative = content.Slice(start).IndexOf(value);
            return relative < 0 ? -1 : start + relative;
        }
    }
}
