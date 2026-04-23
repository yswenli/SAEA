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
*唯一标识：726168cb-71b3-42e9-8b16-dfe0424060a5
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：
*
*=====================================================================
*修改标记
*修改时间：2019/11/29 20:45:22
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
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

        public static Domain FromString(string domain)
        {
            return new Domain(domain);
        }

        public static Domain FromArray(byte[] message, int offset)
        {
            return FromArray(message, offset, out offset);
        }

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

        public Domain(byte[][] labels)
        {
            this.labels = labels;
        }

        public Domain(string[] labels, Encoding encoding)
        {
            this.labels = labels.Select(label => encoding.GetBytes(label)).ToArray();
        }

        public Domain(string domain) : this(domain.Split('.')) { }

        public Domain(string[] labels) : this(labels, Encoding.ASCII) { }

        public int Size
        {
            get { return labels.Sum(l => l.Length) + labels.Length + 1; }
        }

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

        public string ToString(Encoding encoding)
        {
            return string.Join(".", labels.Select(label => encoding.GetString(label)));
        }

        public override string ToString()
        {
            return ToString(Encoding.ASCII);
        }

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
