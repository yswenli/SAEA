using System;
using System.Buffers;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 第一层编解码：帧字节与信封之间转换。分帧器产出的帧交给它。
    /// </summary>
    public interface ICodec
    {
        /// <summary>
        /// 把帧解码为信封对象。入参是分帧器产出的帧切片，
        /// 其偏移由分帧配置决定，实现不得自带包头偏移假设。
        /// 实现不得保留该内存：返回后调用方会立即释放其背衬，
        /// 需要留存的部分必须复制到独立内存。
        /// </summary>
        object Decode(ReadOnlyMemory<byte> frame);

        /// <summary>把消息编码为 body，只写 body，不写包头。</summary>
        void Encode(object message, IBufferWriter<byte> writer);
    }
}