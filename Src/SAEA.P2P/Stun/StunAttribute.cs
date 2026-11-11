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
*命名空间：SAEA.P2P.Stun
*文件名： StunAttribute
*版本号： v26.11.5.1
*唯一标识：e528b0ce-6498-4e4b-9d5c-57d7bb72ec0f
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 15:20:00
*描述：STUN 属性值模型
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 15:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 STUN 属性值模型
*
*****************************************************************************/
using System;

namespace SAEA.P2P.Stun
{
    /// <summary>
    /// STUN 属性：类型加未补齐的值区；补齐只在编码时追加，不进入值本身。
    /// 类型按 ushort 保存，以便原样保留本实现不认识的 comprehension-optional 属性。
    /// </summary>
    public sealed class StunAttribute
    {
        /// <summary>
        /// 未补齐的属性值
        /// </summary>
        private readonly byte[] _value;

        /// <summary>
        /// 用原始类型创建属性，用于保存未知属性
        /// </summary>
        /// <param name="type">属性类型原值</param>
        /// <param name="value">未补齐的属性值</param>
        public StunAttribute(ushort type, byte[] value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            Type = type;
            _value = (byte[])value.Clone();
        }

        /// <summary>
        /// 用已知类型创建属性
        /// </summary>
        /// <param name="type">属性类型</param>
        /// <param name="value">未补齐的属性值</param>
        public StunAttribute(StunAttributeType type, byte[] value)
            : this((ushort)type, value)
        {
        }

        /// <summary>
        /// 属性类型原值
        /// </summary>
        public ushort Type { get; }

        /// <summary>
        /// 未补齐的属性值；返回只读视图，底层数组不会被外部改写
        /// </summary>
        public ReadOnlyMemory<byte> Value
        {
            get { return _value; }
        }

        /// <summary>
        /// 值区补齐到 4 字节倍数后的长度
        /// </summary>
        public int PaddedValueLength
        {
            get { return (_value.Length + 3) & ~3; }
        }

        /// <summary>
        /// 整个属性在报文中的长度，含 4 字节属性头与补齐字节
        /// </summary>
        public int EncodedLength
        {
            get { return StunConstants.AttributeHeaderLength + PaddedValueLength; }
        }

        /// <summary>
        /// 是否为 comprehension-required 属性
        /// </summary>
        public bool IsComprehensionRequired
        {
            get { return (Type & 0x8000) == 0; }
        }
    }
}