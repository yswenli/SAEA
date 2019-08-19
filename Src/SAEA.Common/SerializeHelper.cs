/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Commom
*文件名： Class1
*版本号： v4.5.6.7
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： v4.5.6.7
*描述：
*
*****************************************************************************/
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;

namespace SAEA.Common
{
    /// <summary>
    /// 常规序列化工具类
    /// </summary>
    public static class SerializeHelper
    {
        /// <summary>
        ///     二进制序列化
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static byte[] ByteSerialize<T>(T obj)
        {
            using (var m = new MemoryStream())
            {
                m.Position = 0;
                var bin = new BinaryFormatter();
                bin.Serialize(m, obj);
                m.Position = 0;
                return m.ToArray();
            }
        }

        /// <summary>
        ///     二进制反序列化
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public static T ByteDeserialize<T>(byte[] buffer)
        {
            using (var m = new MemoryStream())
            {
                m.Position = 0;
                m.Write(buffer, 0, buffer.Length);
                var bin = new BinaryFormatter();
                m.Position = 0;
                return (T)bin.Deserialize(m);
            }
        }


        #region byte序列化扩展
        public static byte[] ToBytes<T>(this T t) where T : class, new()
        {
            return ByteSerialize<T>(t);
        }

        public static byte[] ToBytes<T>(this List<T> t) where T : class, new()
        {
            return ByteSerialize(t);
        }

        public static byte[] ToBytes(this List<byte[]> t)
        {
            return ByteSerialize(t);
        }

        public static T ToInstance<T>(this byte[] buffer) where T : class, new()
        {
            return ByteDeserialize<T>(buffer);
        }

        public static List<T> ToList<T>(this byte[] buffer) where T : class, new()
        {
            return ByteDeserialize<List<T>>(buffer);
        }
        public static List<byte[]> ToList(this byte[] buffer)
        {
            return ByteDeserialize<List<byte[]>>(buffer);
        }
        #endregion

        #region json
        /// <summary>
        /// newton.json序列化
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="expended"></param>
        /// <returns></returns>
        public static string Serialize(object obj, bool expended = false)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
            settings.DateFormatString = "yyyy-MM-dd HH:mm:ss.fff";
            if (expended)
            {
                settings.Formatting = Formatting.Indented;
            }
            return JsonConvert.SerializeObject(obj, settings);
        }

        /// <summary>
        ///     newton.json反序列化
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="json"></param>
        /// <returns></returns>
        public static T Deserialize<T>(string json)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
            settings.DateFormatString = "yyyy-MM-dd HH:mm:ss.fff";
            return JsonConvert.DeserializeObject<T>(json, settings);
        }
        #endregion

        #region ProtoBuffer

        /// <summary>
        /// 序列化
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="t"></param>
        /// <returns></returns>
        public static byte[] PBSerialize<T>(T t)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ProtoBuf.Serializer.Serialize<T>(ms, t);
                byte[] result = new byte[ms.Length];
                ms.Position = 0;
                ms.Read(result, 0, result.Length);
                return result;
            }
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        public static T PBDeserialize<T>(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(data, 0, data.Length);
                ms.Position = 0;
                return (T)ProtoBuf.Serializer.Deserialize<T>(ms);
            }
        }

        #endregion

    }

    #region pb attributes

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Interface | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class ProtoContractAttribute : Attribute
    {
        private int implicitFirstTag;

        private ushort flags;

        private const ushort OPTIONS_InferTagFromName = 1;

        private const ushort OPTIONS_InferTagFromNameHasValue = 2;

        private const ushort OPTIONS_UseProtoMembersOnly = 4;

        private const ushort OPTIONS_SkipConstructor = 8;

        private const ushort OPTIONS_IgnoreListHandling = 16;

        private const ushort OPTIONS_AsReferenceDefault = 32;

        private const ushort OPTIONS_EnumPassthru = 64;

        private const ushort OPTIONS_EnumPassthruHasValue = 128;

        private const ushort OPTIONS_IsGroup = 256;

        /// <summary>
        /// Gets or sets the defined name of the type.
        /// </summary>
        public string Name
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the fist offset to use with implicit field tags;
        /// only uesd if ImplicitFields is set.
        /// </summary>
        public int ImplicitFirstTag
        {
            get
            {
                return implicitFirstTag;
            }
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException("ImplicitFirstTag");
                }
                implicitFirstTag = value;
            }
        }

        /// <summary>
        /// If specified, alternative contract markers (such as markers for XmlSerailizer or DataContractSerializer) are ignored.
        /// </summary>
        public bool UseProtoMembersOnly
        {
            get
            {
                return HasFlag(4);
            }
            set
            {
                SetFlag(4, value);
            }
        }

        /// <summary>
        /// If specified, do NOT treat this type as a list, even if it looks like one.
        /// </summary>
        public bool IgnoreListHandling
        {
            get
            {
                return HasFlag(16);
            }
            set
            {
                SetFlag(16, value);
            }
        }

        /// <summary>
        /// Gets or sets the mechanism used to automatically infer field tags
        /// for members. This option should be used in advanced scenarios only.
        /// Please review the important notes against the ImplicitFields enumeration.
        /// </summary>
        public ProtoBuf.ImplicitFields ImplicitFields
        {
            get;
            set;
        }

        /// <summary>
        /// Enables/disables automatic tag generation based on the existing name / order
        /// of the defined members. This option is not used for members marked
        /// with ProtoMemberAttribute, as intended to provide compatibility with
        /// WCF serialization. WARNING: when adding new fields you must take
        /// care to increase the Order for new elements, otherwise data corruption
        /// may occur.
        /// </summary>
        /// <remarks>If not explicitly specified, the default is assumed from Serializer.GlobalOptions.InferTagFromName.</remarks>
        public bool InferTagFromName
        {
            get
            {
                return HasFlag(1);
            }
            set
            {
                SetFlag(1, value);
                SetFlag(2, true);
            }
        }

        /// <summary>
        /// Has a InferTagFromName value been explicitly set? if not, the default from the type-model is assumed.
        /// </summary>
        internal bool InferTagFromNameHasValue => HasFlag(2);

        /// <summary>
        /// Specifies an offset to apply to [DataMember(Order=...)] markers;
        /// this is useful when working with mex-generated classes that have
        /// a different origin (usually 1 vs 0) than the original data-contract.
        ///
        /// This value is added to the Order of each member.
        /// </summary>
        public int DataMemberOffset
        {
            get;
            set;
        }

        /// <summary>
        /// If true, the constructor for the type is bypassed during deserialization, meaning any field initializers
        /// or other initialization code is skipped.
        /// </summary>
        public bool SkipConstructor
        {
            get
            {
                return HasFlag(8);
            }
            set
            {
                SetFlag(8, value);
            }
        }

        /// <summary>
        /// Should this type be treated as a reference by default? Please also see the implications of this,
        /// as recorded on ProtoMemberAttribute.AsReference
        /// </summary>
        public bool AsReferenceDefault
        {
            get
            {
                return HasFlag(32);
            }
            set
            {
                SetFlag(32, value);
            }
        }

        /// <summary>
        /// Indicates whether this type should always be treated as a "group" (rather than a string-prefixed sub-message)
        /// </summary>
        public bool IsGroup
        {
            get
            {
                return HasFlag(256);
            }
            set
            {
                SetFlag(256, value);
            }
        }

        /// <summary>
        /// Applies only to enums (not to DTO classes themselves); gets or sets a value indicating that an enum should be treated directly as an int/short/etc, rather
        /// than enforcing .proto enum rules. This is useful *in particul* for [Flags] enums.
        /// </summary>
        public bool EnumPassthru
        {
            get
            {
                return HasFlag(64);
            }
            set
            {
                SetFlag(64, value);
                SetFlag(128, true);
            }
        }

        /// <summary>
        /// Allows to define a surrogate type used for serialization/deserialization purpose.
        /// </summary>
        public Type Surrogate
        {
            get;
            set;
        }

        /// <summary>
        /// Has a EnumPassthru value been explicitly set?
        /// </summary>
        internal bool EnumPassthruHasValue => HasFlag(128);

        private bool HasFlag(ushort flag)
        {
            return (flags & flag) == flag;
        }

        private void SetFlag(ushort flag, bool value)
        {
            if (value)
            {
                flags |= flag;
            }
            else
            {
                flags &= (ushort)(~flag);
            }
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class ProtoMemberAttribute : Attribute, IComparable, IComparable<ProtoMemberAttribute>
    {
        internal MemberInfo Member;

        internal MemberInfo BackingMember;

        internal bool TagIsPinned;

        private string name;

        private ProtoBuf.DataFormat dataFormat;

        private int tag;

        private ProtoBuf.MemberSerializationOptions options;

        /// <summary>
        /// Gets or sets the original name defined in the .proto; not used
        /// during serialization.
        /// </summary>
        public string Name
        {
            get
            {
                return name;
            }
            set
            {
                name = value;
            }
        }

        /// <summary>
        /// Gets or sets the data-format to be used when encoding this value.
        /// </summary>
        public ProtoBuf.DataFormat DataFormat
        {
            get
            {
                return dataFormat;
            }
            set
            {
                dataFormat = value;
            }
        }

        /// <summary>
        /// Gets the unique tag used to identify this member within the type.
        /// </summary>
        public int Tag => tag;

        /// <summary>
        /// Gets or sets a value indicating whether this member is mandatory.
        /// </summary>
        public bool IsRequired
        {
            get
            {
                return (options & ProtoBuf.MemberSerializationOptions.Required) == ProtoBuf.MemberSerializationOptions.Required;
            }
            set
            {
                if (value)
                {
                    options |= ProtoBuf.MemberSerializationOptions.Required;
                }
                else
                {
                    options &= ~ProtoBuf.MemberSerializationOptions.Required;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether this member is packed.
        /// This option only applies to list/array data of primitive types (int, double, etc).
        /// </summary>
        public bool IsPacked
        {
            get
            {
                return (options & ProtoBuf.MemberSerializationOptions.Packed) == ProtoBuf.MemberSerializationOptions.Packed;
            }
            set
            {
                if (value)
                {
                    options |= ProtoBuf.MemberSerializationOptions.Packed;
                }
                else
                {
                    options &= ~ProtoBuf.MemberSerializationOptions.Packed;
                }
            }
        }

        /// <summary>
        /// Indicates whether this field should *repace* existing values (the default is false, meaning *append*).
        /// This option only applies to list/array data.
        /// </summary>
        public bool OverwriteList
        {
            get
            {
                return (options & ProtoBuf.MemberSerializationOptions.OverwriteList) == ProtoBuf.MemberSerializationOptions.OverwriteList;
            }
            set
            {
                if (value)
                {
                    options |= ProtoBuf.MemberSerializationOptions.OverwriteList;
                }
                else
                {
                    options &= ~ProtoBuf.MemberSerializationOptions.OverwriteList;
                }
            }
        }

        /// <summary>
        /// Enables full object-tracking/full-graph support.
        /// </summary>
        public bool AsReference
        {
            get
            {
                return (options & ProtoBuf.MemberSerializationOptions.AsReference) == ProtoBuf.MemberSerializationOptions.AsReference;
            }
            set
            {
                if (value)
                {
                    options |= ProtoBuf.MemberSerializationOptions.AsReference;
                }
                else
                {
                    options &= ~ProtoBuf.MemberSerializationOptions.AsReference;
                }
                options |= ProtoBuf.MemberSerializationOptions.AsReferenceHasValue;
            }
        }

        internal bool AsReferenceHasValue
        {
            get
            {
                return (options & ProtoBuf.MemberSerializationOptions.AsReferenceHasValue) == ProtoBuf.MemberSerializationOptions.AsReferenceHasValue;
            }
            set
            {
                if (value)
                {
                    options |= ProtoBuf.MemberSerializationOptions.AsReferenceHasValue;
                }
                else
                {
                    options &= ~ProtoBuf.MemberSerializationOptions.AsReferenceHasValue;
                }
            }
        }

        /// <summary>
        /// Embeds the type information into the stream, allowing usage with types not known in advance.
        /// </summary>
        public bool DynamicType
        {
            get
            {
                return (options & ProtoBuf.MemberSerializationOptions.DynamicType) == ProtoBuf.MemberSerializationOptions.DynamicType;
            }
            set
            {
                if (value)
                {
                    options |= ProtoBuf.MemberSerializationOptions.DynamicType;
                }
                else
                {
                    options &= ~ProtoBuf.MemberSerializationOptions.DynamicType;
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this member is packed (lists/arrays).
        /// </summary>
        public ProtoBuf.MemberSerializationOptions Options
        {
            get
            {
                return options;
            }
            set
            {
                options = value;
            }
        }

        /// <summary>
        /// Compare with another ProtoMemberAttribute for sorting purposes
        /// </summary>
        public int CompareTo(object other)
        {
            return CompareTo(other as ProtoMemberAttribute);
        }

        /// <summary>
        /// Compare with another ProtoMemberAttribute for sorting purposes
        /// </summary>
        public int CompareTo(ProtoMemberAttribute other)
        {
            if (other == null)
            {
                return -1;
            }
            if (this == other)
            {
                return 0;
            }
            int num = tag.CompareTo(other.tag);
            if (num == 0)
            {
                num = string.CompareOrdinal(name, other.name);
            }
            return num;
        }

        /// <summary>
        /// Creates a new ProtoMemberAttribute instance.
        /// </summary>
        /// <param name="tag">Specifies the unique tag used to identify this member within the type.</param>
        public ProtoMemberAttribute(int tag)
            : this(tag, false)
        {
        }

        internal ProtoMemberAttribute(int tag, bool forced)
        {
            if (tag <= 0 && !forced)
            {
                throw new ArgumentOutOfRangeException("tag");
            }
            this.tag = tag;
        }

        internal void Rebase(int tag)
        {
            this.tag = tag;
        }
    }

    #endregion


}
