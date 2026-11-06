using System;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 帧类型码与 CLR 类型之间的唯一双向映射。
    /// </summary>
    public interface ITypeRegistry
    {
        /// <summary>
        /// 双向注册类型码与 CLR 类型。类型码必须落在 0..255（线上占 1 字节）；
        /// 同一类型码或同一 CLR 类型重复注册抛 <see cref="InvalidOperationException"/>，
        /// 不做覆盖，避免两份映射悄悄漂移。
        /// </summary>
        void Register(int typeCode, Type clrType);

        /// <summary>按类型码查 CLR 类型；未注册返回 null。</summary>
        Type Resolve(int typeCode);

        /// <summary>按 CLR 类型查类型码；未注册返回 -1。</summary>
        int ResolveCode(Type clrType);
    }
}