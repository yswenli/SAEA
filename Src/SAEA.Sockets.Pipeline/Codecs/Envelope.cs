namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 两级编解码之间的中间载体：1 字节类型码 + 独立 payload 拷贝。
    /// 由第一层 codec 从帧中切出，交给路由层按 type 决定第二层 codec。
    /// </summary>
    public sealed class Envelope
    {
        /// <summary>
        /// 线上 1 字节类型码。取值范围由类型注册表约束（0..255），
        /// 信封本身只做承载，不做越界校验。
        /// </summary>
        public int Type { get; set; }

        /// <summary>
        /// 独立堆内存拷贝，绝不指向 PooledMessage 的池化背衬。
        /// 出站编码时由 EnvelopeWrapHandler 填充；入站解码时由 codec 拷贝。
        /// </summary>
        public byte[] Payload { get; set; }
    }
}