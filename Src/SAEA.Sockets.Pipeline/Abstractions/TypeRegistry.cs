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
*文件名： TypeRegistry
*版本号： v26.11.5.1
*唯一标识：cae3a61b-a5c7-4407-9a7f-f5830cab8bec
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/6 09:50:56
*描述：线程安全的类型注册表：注册只在启动期发生，读路径无锁
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 09:50:56
*修改人： yswenli
*版本号： v26.11.5.1
*描述：线程安全的类型注册表：注册只在启动期发生，读路径无锁
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;

namespace SAEA.Sockets.Pipeline
{
    /// <summary>
    /// 线程安全的类型注册表：注册只在启动期发生，读路径无锁。
    /// </summary>
    public sealed class TypeRegistry : ITypeRegistry
    {
        private readonly object _gate = new object();
        private readonly ConcurrentDictionary<int, Type> _codeToType = new ConcurrentDictionary<int, Type>();
        private readonly ConcurrentDictionary<Type, int> _typeToCode = new ConcurrentDictionary<Type, int>();

        /// <summary>
        /// 双向注册。类型码越界抛 <see cref="ArgumentOutOfRangeException"/>，
        /// 重复注册抛 <see cref="InvalidOperationException"/>。
        /// 写入失败时回滚已写入的一侧，保证两个方向始终一致。
        /// </summary>
        /// <param name="typeCode">线上帧头的类型码，取值 0..255。</param>
        /// <param name="clrType">对应的 CLR 类型，不能为 null。</param>
        public void Register(int typeCode, Type clrType)
        {
            if (clrType == null)
                throw new ArgumentNullException(nameof(clrType));
            if (typeCode < 0 || typeCode > 255)
                throw new ArgumentOutOfRangeException(nameof(typeCode), "type code 必须落在 0..255（线上 1 字节）。");

            lock (_gate)
            {
                if (_codeToType.ContainsKey(typeCode))
                    throw new InvalidOperationException("type code " + typeCode + " 已注册。");
                if (_typeToCode.ContainsKey(clrType))
                    throw new InvalidOperationException("CLR 类型 " + clrType.FullName + " 已注册。");

                if (!_codeToType.TryAdd(typeCode, clrType))
                    throw new InvalidOperationException("type code " + typeCode + " 已注册。");

                if (!_typeToCode.TryAdd(clrType, typeCode))
                {
                    Type ignored;
                    _codeToType.TryRemove(typeCode, out ignored);
                    throw new InvalidOperationException("CLR 类型 " + clrType.FullName + " 已注册。");
                }
            }
        }

        /// <summary>按类型码查 CLR 类型；未注册返回 null。</summary>
        /// <param name="typeCode">线上帧头的类型码。</param>
        /// <returns>对应的 CLR 类型；未注册时为 null。</returns>
        public Type Resolve(int typeCode)
        {
            Type type;
            return _codeToType.TryGetValue(typeCode, out type) ? type : null;
        }

        /// <summary>按 CLR 类型查类型码；未注册返回 -1。</summary>
        /// <param name="clrType">待查询的 CLR 类型；为 null 时返回 -1。</param>
        /// <returns>对应的类型码；未注册时为 -1。</returns>
        public int ResolveCode(Type clrType)
        {
            int code;
            return clrType != null && _typeToCode.TryGetValue(clrType, out code) ? code : -1;
        }
    }
}