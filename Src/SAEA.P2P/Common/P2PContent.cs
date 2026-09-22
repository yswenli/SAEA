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
            if (start < 0 || start > content.Length) return -1;
            var relative = content.Slice(start).IndexOf(value);
            return relative < 0 ? -1 : start + relative;
        }
    }
}