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
*文件名： CharacterString
*版本号： v26.4.23.1
*唯一标识：477fdc96-c046-4cdd-ab29-35adf220f2f6
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：CharacterString接口
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
using System.Text;

namespace SAEA.DNS.Protocol
{

    /// <summary>
    /// RFC1035 第 3.3 节定义的长度前缀字符字符串
    /// </summary>
    public class CharacterString
    {
        private const int MAX_SIZE = byte.MaxValue;

        private byte[] data;

        /// <summary>
        /// 从指定偏移解析消息中剩余的全部字符字符串
        /// </summary>
        /// <param name="message">消息字节</param>
        /// <param name="offset">起始偏移</param>
        /// <returns>解析出的字符字符串集合</returns>
        public static IList<CharacterString> GetAllFromArray(byte[] message, int offset)
        {
            return GetAllFromArray(message, offset, out offset);
        }

        /// <summary>
        /// 从指定偏移解析消息中剩余的全部字符字符串，并返回结束偏移
        /// </summary>
        /// <param name="message">消息字节</param>
        /// <param name="offset">起始偏移</param>
        /// <param name="endOffset">解析结束后的偏移</param>
        /// <returns>解析出的字符字符串集合</returns>
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

        /// <summary>
        /// 从指定偏移读取一个字符字符串
        /// </summary>
        /// <param name="message">消息字节</param>
        /// <param name="offset">起始偏移，首字节为字符长度</param>
        /// <returns>解析出的字符字符串</returns>
        public static CharacterString FromArray(byte[] message, int offset)
        {
            return FromArray(message, offset, out offset);
        }

        /// <summary>
        /// 从指定偏移读取一个字符字符串，并返回结束偏移
        /// </summary>
        /// <param name="message">消息字节</param>
        /// <param name="offset">起始偏移，首字节为字符长度</param>
        /// <param name="endOffset">解析结束后的偏移</param>
        /// <returns>解析出的字符字符串</returns>
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

        /// <summary>
        /// 以 ASCII 编码把文本拆分为字符字符串集合
        /// </summary>
        /// <param name="message">待拆分的文本</param>
        /// <returns>每段不超过 255 字节的字符字符串集合，空文本返回空集合</returns>
        public static IList<CharacterString> FromString(string message)
        {
            return FromString(message, Encoding.ASCII);
        }

        /// <summary>
        /// 以指定编码把文本拆分为字符字符串集合
        /// </summary>
        /// <param name="message">待拆分的文本</param>
        /// <param name="encoding">拆分前使用的编码</param>
        /// <returns>每段不超过 255 字节的字符字符串集合，空文本返回空集合</returns>
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

        /// <summary>
        /// 以原始字节初始化字符字符串，超过 255 字节时截断
        /// </summary>
        /// <param name="data">字符内容字节</param>
        public CharacterString(byte[] data)
        {
            if (data.Length > MAX_SIZE) Array.Resize(ref data, MAX_SIZE);
            this.data = data;
        }

        /// <summary>
        /// 以指定编码把文本编码后初始化字符字符串
        /// </summary>
        /// <param name="data">字符内容</param>
        /// <param name="encoding">编码方式</param>
        public CharacterString(string data, Encoding encoding) : this(encoding.GetBytes(data)) { }

        /// <summary>
        /// 以 ASCII 编码把文本编码后初始化字符字符串
        /// </summary>
        /// <param name="data">字符内容</param>
        public CharacterString(string data) : this(data, Encoding.ASCII) { }

        /// <summary>
        /// 编码后的字节长度，含 1 字节长度前缀
        /// </summary>
        public int Size
        {
            get { return data.Length + 1; }
        }

        /// <summary>
        /// 序列化为长度前缀加字符内容
        /// </summary>
        /// <returns>序列化后的字节</returns>
        public byte[] ToArray()
        {
            byte[] result = new byte[Size];
            result[0] = (byte)data.Length;
            data.CopyTo(result, 1);
            return result;
        }

        /// <summary>
        /// 以指定编码把字符内容解码为文本
        /// </summary>
        /// <param name="encoding">解码方式</param>
        /// <returns>解码后的文本</returns>
        public string ToString(Encoding encoding)
        {
            return encoding.GetString(data);
        }

        /// <summary>
        /// 以 ASCII 编码把字符内容解码为文本
        /// </summary>
        /// <returns>解码后的文本</returns>
        public override string ToString()
        {
            return ToString(Encoding.ASCII);
        }
    }
}
