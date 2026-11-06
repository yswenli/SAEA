namespace SAEA.Sockets.Pipeline
{
    /// <summary>线格式常量：8B 小端 body 长度 + 1B type + body。</summary>
    public static class FrameConstants
    {
        /// <summary>长度字段占用的字节数。</summary>
        public const int P_LEN = 8;

        /// <summary>类型字段占用的字节数。</summary>
        public const int P_Type = 1;

        /// <summary>帧头总字节数：长度字段 + 类型字段。</summary>
        public const int P_Head = P_LEN + P_Type;
    }
}