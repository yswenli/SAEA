/****************************************************************************
 * 
   ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| _f 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
   ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ _f 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.DNS.Protocol
*文件名： CharacterString
*版本号： v26.4.23.1
*唯一标识：bd94a05f-aa49-49f3-bf2e-7ba07361172d
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
using System.Text;

namespace SAEA.DNS.Protocol
{

    /// <summary>
    /// RFC1035����3.3�£��ж���ġ��ַ��������ն�ʵ��
    /// </summary>
    public class CharacterString
    {
        private const int MAX_SIZE = byte.MaxValue;

        private byte[] data;

        public static IList<CharacterString> GetAllFromArray(byte[] message, int offset)
        {
            return GetAllFromArray(message, offset, out offset);
        }

        public static IList<CharacterString> GetAllFromArray(byte[] message, int offset, out int endOffset)
        {
            IList<CharacterString> characterStrings = new List<CharacterString>();

            while (offset < message.Length)
            {
                characterStrings.Add(CharacterString.FromArray(message, offset, out offset));
            }

            endOffset = offset;
            return characterStrings;
        }

        public static CharacterString FromArray(byte[] message, int offset)
        {
            return FromArray(message, offset, out offset);
        }

        public static CharacterString FromArray(byte[] message, int offset, out int endOffset)
        {
            if (message.Length < 1)
            {
                throw new ArgumentException("Empty message");
            }

            byte len = message[offset++];
            byte[] data = new byte[len];
            Buffer.BlockCopy(message, offset, data, 0, len);
            endOffset = offset + len;
            return new CharacterString(data);
        }

        public static IList<CharacterString> FromString(string message)
        {
            return FromString(message, Encoding.ASCII);
        }

        public static IList<CharacterString> FromString(string message, Encoding encoding)
        {
            byte[] bytes = encoding.GetBytes(message);
            int size = (int)Math.Ceiling((double)bytes.Length / MAX_SIZE);
            IList<CharacterString> characterStrings = new List<CharacterString>(size);

            for (int i = 0; i < bytes.Length; i += MAX_SIZE)
            {
                int len = Math.Min(bytes.Length - i, MAX_SIZE);
                byte[] chunk = new byte[len];
                Buffer.BlockCopy(bytes, i, chunk, 0, len);
                characterStrings.Add(new CharacterString(chunk));
            }

            return characterStrings;
        }

        public CharacterString(byte[] data)
        {
            if (data.Length > MAX_SIZE) Array.Resize(ref data, MAX_SIZE);
            this.data = data;
        }

        public CharacterString(string data, Encoding encoding) : this(encoding.GetBytes(data)) { }

        public CharacterString(string data) : this(data, Encoding.ASCII) { }

        public int Size
        {
            get { return data.Length + 1; }
        }

        public byte[] ToArray()
        {
            byte[] result = new byte[Size];
            result[0] = (byte)data.Length;
            data.CopyTo(result, 1);
            return result;
        }

        public string ToString(Encoding encoding)
        {
            return encoding.GetString(data);
        }

        public override string ToString()
        {
            return ToString(Encoding.ASCII);
        }
    }
}
