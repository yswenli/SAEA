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
*文件名： Question
*版本号： v26.4.23.1
*唯一标识：ad8607d1-15a1-4271-a425-167076a39ae3
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：Question接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:47
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Common;
using SAEA.DNS.Common.Utils;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SAEA.DNS.Protocol
{
    /// <summary>
    /// 问题
    /// </summary>
    public class Question : IMessageEntry
    {
        /// <summary>
        /// 从指定偏移连续解析指定数量的问题
        /// </summary>
        /// <param name="message">消息字节</param>
        /// <param name="offset">起始偏移</param>
        /// <param name="questionCount">待解析的问题数量</param>
        /// <returns>解析出的问题集合</returns>
        public static IList<Question> GetAllFromArray(byte[] message, int offset, int questionCount)
        {
            return GetAllFromArray(message, offset, questionCount, out offset);
        }

        /// <summary>
        /// 从指定偏移连续解析指定数量的问题，并返回结束偏移
        /// </summary>
        /// <param name="message">消息字节</param>
        /// <param name="offset">起始偏移</param>
        /// <param name="questionCount">待解析的问题数量</param>
        /// <param name="endOffset">解析结束后的偏移</param>
        /// <returns>解析出的问题集合</returns>
        public static IList<Question> GetAllFromArray(byte[] message, int offset, int questionCount, out int endOffset)
        {
            IList<Question> questions = new List<Question>(questionCount);

            for (int i = 0; i < questionCount; i++)
            {
                questions.Add(FromArray(message, offset, out offset));
            }

            endOffset = offset;
            return questions;
        }

        /// <summary>
        /// 从指定偏移解析单个问题
        /// </summary>
        /// <param name="message">消息字节</param>
        /// <param name="offset">起始偏移</param>
        /// <returns>解析出的问题</returns>
        public static Question FromArray(byte[] message, int offset)
        {
            return FromArray(message, offset, out offset);
        }

        /// <summary>
        /// 从指定偏移解析单个问题，并返回结束偏移
        /// </summary>
        /// <param name="message">消息字节</param>
        /// <param name="offset">起始偏移</param>
        /// <param name="endOffset">解析结束后的偏移</param>
        /// <returns>解析出的问题</returns>
        public static Question FromArray(byte[] message, int offset, out int endOffset)
        {
            Domain domain = Domain.FromArray(message, offset, out offset);

            Tail tail = StructHelper.GetStruct<Tail>(message, offset, Tail.SIZE);

            endOffset = offset + Tail.SIZE;

            return new Question(domain, tail.Type, tail.Class);
        }

        private Domain domain;
        private RecordType type;
        private RecordClass klass;

        /// <summary>
        /// 以域名、记录类型和类别初始化问题
        /// </summary>
        /// <param name="domain">查询域名</param>
        /// <param name="type">记录类型，默认 A</param>
        /// <param name="klass">记录类别，默认 IN</param>
        public Question(Domain domain, RecordType type = RecordType.A, RecordClass klass = RecordClass.IN)
        {
            this.domain = domain;
            this.type = type;
            this.klass = klass;
        }

        /// <summary>
        /// 查询域名
        /// </summary>
        public Domain Name
        {
            get { return domain; }
        }

        /// <summary>
        /// 查询的记录类型
        /// </summary>
        public RecordType Type
        {
            get { return type; }
        }

        /// <summary>
        /// 查询的记录类别
        /// </summary>
        public RecordClass Class
        {
            get { return klass; }
        }

        /// <summary>
        /// 序列化后的字节数，即域名长度加 4 字节类型与类别
        /// </summary>
        public int Size
        {
            get { return domain.Size + Tail.SIZE; }
        }

        /// <summary>
        /// 序列化为域名加类型与类别
        /// </summary>
        /// <returns>问题字节</returns>
        public byte[] ToArray()
        {
            ByteStream result = new ByteStream(Size);

            result
                .Append(domain.ToArray())
                .Append(StructHelper.GetBytes(new Tail { Type = Type, Class = Class }));

            return result.ToArray();
        }

        /// <summary>
        /// 输出域名、类型与类别的可读文本
        /// </summary>
        /// <returns>可读的问题描述</returns>
        public override string ToString()
        {
            return ObjectStringifier.New(this)
                .Add("Name", "Type", "Class")
                .ToString();
        }

        [Endian(EndianOrder.Big)]
        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct Tail
        {
            public const int SIZE = 4;

            private ushort type;
            private ushort klass;

            public RecordType Type
            {
                get { return (RecordType)type; }
                set { type = (ushort)value; }
            }

            public RecordClass Class
            {
                get { return (RecordClass)klass; }
                set { klass = (ushort)value; }
            }
        }
    }
}
