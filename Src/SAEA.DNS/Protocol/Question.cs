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
*唯一标识：454b60fa-b860-4e92-bd00-96a96623b7d1
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
        public static IList<Question> GetAllFromArray(byte[] message, int offset, int questionCount)
        {
            return GetAllFromArray(message, offset, questionCount, out offset);
        }

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

        public static Question FromArray(byte[] message, int offset)
        {
            return FromArray(message, offset, out offset);
        }

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

        public Question(Domain domain, RecordType type = RecordType.A, RecordClass klass = RecordClass.IN)
        {
            this.domain = domain;
            this.type = type;
            this.klass = klass;
        }

        public Domain Name
        {
            get { return domain; }
        }

        public RecordType Type
        {
            get { return type; }
        }

        public RecordClass Class
        {
            get { return klass; }
        }

        public int Size
        {
            get { return domain.Size + Tail.SIZE; }
        }

        public byte[] ToArray()
        {
            ByteStream result = new ByteStream(Size);

            result
                .Append(domain.ToArray())
                .Append(StructHelper.GetBytes(new Tail { Type = Type, Class = Class }));

            return result.ToArray();
        }

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
