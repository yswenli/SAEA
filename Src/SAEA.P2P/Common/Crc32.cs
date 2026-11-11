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
*命名空间：SAEA.P2P.Common
*文件名： Crc32
*版本号： v26.11.5.1
*唯一标识：865ce0cc-d6b5-47d1-9caa-df1371bfe07f
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 15:45:00
*描述：表驱动 CRC-32 计算
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 15:45:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增表驱动 CRC-32 计算
*
*****************************************************************************/
using System;

namespace SAEA.P2P.Common
{
    /// <summary>
    /// CRC-32 计算，采用 IEEE 802.3 反射多项式 0xEDB88320，与 STUN FINGERPRINT 一致。
    /// </summary>
    public static class Crc32
    {
        /// <summary>
        /// 按字节查表，初值与结果都取反
        /// </summary>
        private static readonly uint[] Table = BuildTable();

        /// <summary>
        /// 计算整段数据的 CRC-32
        /// </summary>
        /// <param name="data">待计算数据</param>
        /// <returns>CRC-32 值</returns>
        public static uint Compute(ReadOnlySpan<byte> data)
        {
            var crc = 0xFFFFFFFFu;
            for (var i = 0; i < data.Length; i++)
            {
                crc = (crc >> 8) ^ Table[(crc ^ data[i]) & 0xFF];
            }
            return ~crc;
        }

        /// <summary>
        /// 计算指定区间数据的 CRC-32
        /// </summary>
        /// <param name="data">待计算数据</param>
        /// <param name="offset">起始位置</param>
        /// <param name="count">字节数</param>
        /// <returns>CRC-32 值</returns>
        public static uint Compute(ReadOnlySpan<byte> data, int offset, int count)
        {
            if (offset < 0 || count < 0 || offset + count > data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            var crc = 0xFFFFFFFFu;
            for (var i = offset; i < offset + count; i++)
            {
                crc = (crc >> 8) ^ Table[(crc ^ data[i]) & 0xFF];
            }
            return ~crc;
        }

        /// <summary>
        /// 生成 256 项查表
        /// </summary>
        /// <returns>查表</returns>
        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                var value = i;
                for (var bit = 0; bit < 8; bit++)
                {
                    value = (value & 1) != 0 ? (value >> 1) ^ 0xEDB88320u : value >> 1;
                }
                table[i] = value;
            }
            return table;
        }
    }
}