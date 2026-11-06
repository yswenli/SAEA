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
*命名空间：SAEA.DNS.Protocol
*文件名： Domain
*版本号： v26.4.23.1
*唯一标识：1d507f42-54f0-4028-94bd-618d48710296
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：Domain接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:47
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using SAEA.DNS.Common.Utils;
using SAEA.Common;

namespace SAEA.DNS.Protocol
{
    /// <summary>
    /// 域名
    /// </summary>
    public class Domain : IComparable<Domain>
    {
        private const byte ASCII_UPPERCASE_FIRST = 65;
        private const byte ASCII_UPPERCASE_LAST = 90;
        private const byte ASCII_LOWERCASE_FIRST = 97;
        private const byte ASCII_LOWERCASE_LAST = 122;
        private const byte ASCII_UPPERCASE_MASK = 223;

        private byte[][] labels;

        /// <summary>
        /// 由域名文本创建域名对象
        /// </summary>
        /// <param name="domain">点分域名，如 www.example.com</param>
        /// <returns>域名对象</returns>
        public static Domain FromString(string domain)
        {
            return new Domain(domain);
        }

        /// <summary>
        /// 从消息指定偏移解析域名
        /// </summary>
        /// <param name="message">消息字节</param>
        /// <param name="offset">起始偏移</param>
        /// <returns>解析出的域名</returns>
        public static Domain FromArray(byte[] message, int offset)
        {
            return FromArray(message, offset, out offset);
        }

        /// <summary>
        /// 从消息指定偏移解析域名，并返回结束偏移，支持压缩指针
        /// </summary>
        /// <param name="message">消息字节</param>
        /// <param name="offset">起始偏移</param>
        /// <param name="endOffset">解析结束后的偏移，遇到压缩指针时为指针之后的位置</param>
        /// <returns>解析出的域名</returns>
        public static Domain FromArray(byte[] message, int offset, out int endOffset)
        {
            IList<byte[]> labels = new List<byte[]>();
            bool endOffsetAssigned = false;
            endOffset = 0;
            byte lengthOrPointer;

            while ((lengthOrPointer = message[offset++]) > 0)
            {
                //设置两个位（指针）
                if (lengthOrPointer.GetBitValueAt(6, 2) == 3)
                {
                    if (!endOffsetAssigned)
                    {
                        endOffsetAssigned = true;
                        endOffset = offset + 1;
                    }

                    ushort pointer = lengthOrPointer.GetBitValueAt(0, 6);
                    offset = (pointer << 8) | message[offset];

                    continue;
                }
                else if (lengthOrPointer.GetBitValueAt(6, 2) != 0)
                {
                    throw new ArgumentException("标签长度中出现意外的位模式");
                }

                byte length = lengthOrPointer;
                byte[] label = new byte[length];
                Array.Copy(message, offset, label, 0, length);

                labels.Add(label);

                offset += length;
            }

            if (!endOffsetAssigned)
            {
                endOffset = offset;
            }

            return new Domain(labels.ToArray());
        }

        /// <summary>
        /// 生成反向解析域名，IPv4 为 in-addr.arpa，IPv6 为 ip6.arpa
        /// </summary>
        /// <param name="ip">待反向解析的 IP 地址</param>
        /// <returns>反向解析域名</returns>
        public static Domain PointerName(IPAddress ip)
        {
            return new Domain(FormatReverseIP(ip));
        }

        private static string FormatReverseIP(IPAddress ip)
        {
            byte[] address = ip.GetAddressBytes();

            if (address.Length == 4)
            {
                return string.Join(".", address.Reverse().Select(b => b.ToString())) + ".in-addr.arpa";
            }

            byte[] nibbles = new byte[address.Length * 2];

            for (int i = 0, j = 0; i < address.Length; i++, j = 2 * i)
            {
                byte b = address[i];

                nibbles[j] = b.GetBitValueAt(4, 4);
                nibbles[j + 1] = b.GetBitValueAt(0, 4);
            }

            return string.Join(".", nibbles.Reverse().Select(b => b.ToString("x"))) + ".ip6.arpa";
        }

        private static bool IsASCIIAlphabet(byte b)
        {
            return (ASCII_UPPERCASE_FIRST <= b && b <= ASCII_UPPERCASE_LAST) ||
                (ASCII_LOWERCASE_FIRST <= b && b <= ASCII_LOWERCASE_LAST);
        }

        private static int CompareTo(byte a, byte b)
        {
            if (IsASCIIAlphabet(a) && IsASCIIAlphabet(b))
            {
                a &= ASCII_UPPERCASE_MASK;
                b &= ASCII_UPPERCASE_MASK;
            }

            return a - b;
        }

        private static int CompareTo(byte[] a, byte[] b)
        {
            int length = Math.Min(a.Length, b.Length);

            for (int i = 0; i < length; i++)
            {
                int v = CompareTo(a[i], b[i]);
                if (v != 0) return v;
            }

            return a.Length - b.Length;
        }

        /// <summary>
        /// 以已编码的标签数组初始化域名
        /// </summary>
        /// <param name="labels">按顺序排列的标签字节</param>
        public Domain(byte[][] labels)
        {
            this.labels = labels;
        }

        /// <summary>
        /// 以指定编码把标签编码后初始化域名
        /// </summary>
        /// <param name="labels">按顺序排列的标签文本</param>
        /// <param name="encoding">标签编码方式</param>
        public Domain(string[] labels, Encoding encoding)
        {
            this.labels = labels.Select(label => encoding.GetBytes(label)).ToArray();
        }

        /// <summary>
        /// 由点分域名文本初始化域名，按 '.' 拆分标签
        /// </summary>
        /// <param name="domain">点分域名</param>
        public Domain(string domain) : this(domain.Split('.')) { }

        /// <summary>
        /// 以 ASCII 编码把标签编码后初始化域名
        /// </summary>
        /// <param name="labels">按顺序排列的标签文本</param>
        public Domain(string[] labels) : this(labels, Encoding.ASCII) { }

        /// <summary>
        /// 序列化后的字节数，含各标签长度字节与末尾结束字节
        /// </summary>
        public int Size
        {
            get { return labels.Sum(l => l.Length) + labels.Length + 1; }
        }

        /// <summary>
        /// 序列化为长度前缀标签加末尾结束字节
        /// </summary>
        /// <returns>序列化后的字节</returns>
        public byte[] ToArray()
        {
            byte[] result = new byte[Size];
            int offset = 0;

            foreach (byte[] label in labels)
            {
                result[offset++] = (byte)label.Length;
                label.CopyTo(result, offset);
                offset += label.Length;
            }

            result[offset] = 0;
            return result;
        }

        /// <summary>
        /// 以指定编码解码各标签并用 '.' 连接为域名文本
        /// </summary>
        /// <param name="encoding">标签解码方式</param>
        /// <returns>点分域名文本</returns>
        public string ToString(Encoding encoding)
        {
            return string.Join(".", labels.Select(label => encoding.GetString(label)));
        }

        /// <summary>
        /// 以 ASCII 编码解码各标签并用 '.' 连接为域名文本
        /// </summary>
        /// <returns>点分域名文本</returns>
        public override string ToString()
        {
            return ToString(Encoding.ASCII);
        }

        /// <summary>
        /// 按标签逐字节比较域名，ASCII 字母不区分大小写
        /// </summary>
        /// <param name="other">待比较的域名</param>
        /// <returns>小于 0 表示当前域名靠前，0 表示相等，大于 0 表示靠后</returns>
        public int CompareTo(Domain other)
        {
            int length = Math.Min(labels.Length, other.labels.Length);

            for (int i = 0; i < length; i++)
            {
                int v = CompareTo(this.labels[i], other.labels[i]);
                if (v != 0) return v;
            }

            return this.labels.Length - other.labels.Length;
        }

        /// <summary>
        /// 按域名比较结果判断相等，null 或非域名对象返回 false
        /// </summary>
        /// <param name="obj">待比较的对象</param>
        /// <returns>域名相同时为 true</returns>
        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }
            if (!(obj is Domain))
            {
                return false;
            }

            return CompareTo(obj as Domain) == 0;
        }

        /// <summary>
        /// 计算域名哈希值，ASCII 字母统一为大写后参与计算
        /// </summary>
        /// <returns>哈希值</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;

                foreach (byte[] label in labels)
                {
                    foreach (byte b in label)
                    {
                        hash = hash * 31 + (IsASCIIAlphabet(b) ? b & ASCII_UPPERCASE_MASK : b);
                    }
                }

                return hash;
            }
        }
    }
}
