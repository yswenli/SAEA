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
*命名空间：SAEA.Sockets.Pipeline
*文件名： ITypeRegistry
*版本号： v26.11.5.1
*唯一标识：8b3bb41e-6e44-4c2d-b031-4499e56d6bee
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：帧类型码与 CLR 类型之间的唯一双向映射
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：帧类型码与 CLR 类型之间的唯一双向映射
*
*****************************************************************************/
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
        /// <param name="typeCode">线上帧头的类型码，取值 0..255。</param>
        /// <param name="clrType">对应的 CLR 类型。</param>
        void Register(int typeCode, Type clrType);

        /// <summary>按类型码查 CLR 类型；未注册返回 null。</summary>
        /// <param name="typeCode">线上帧头的类型码。</param>
        /// <returns>对应的 CLR 类型；未注册时为 null。</returns>
        Type Resolve(int typeCode);

        /// <summary>按 CLR 类型查类型码；未注册返回 -1。</summary>
        /// <param name="clrType">待查询的 CLR 类型。</param>
        /// <returns>对应的类型码；未注册时为 -1。</returns>
        int ResolveCode(Type clrType);
    }
}