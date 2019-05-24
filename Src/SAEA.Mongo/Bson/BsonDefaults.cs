/****************************************************************************
*项目名称：SAEA.Mongo.Bson
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Mongo.Bson
*类 名 称：BsonDefaults
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/5/22 11:09:39
*描述：
*=====================================================================
*修改时间：2019/5/22 11:09:39
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Mongo.Bson.Serialization;
using System.Collections.Generic;
using System.Dynamic;

namespace SAEA.Mongo.Bson
{
    /// <summary>
    /// A static helper class containing BSON defaults.
    /// </summary>
    public static class BsonDefaults
    {
        // private static fields
        private static bool __dynamicArraySerializerWasSet;
        private static IBsonSerializer __dynamicArraySerializer;
        private static bool __dynamicDocumentSerializerWasSet;
        private static IBsonSerializer __dynamicDocumentSerializer;
        private static GuidRepresentation __guidRepresentation = GuidRepresentation.CSharpLegacy;
        private static int __maxDocumentSize = int.MaxValue;
        private static int __maxSerializationDepth = 100;

        // public static properties
        /// <summary>
        /// Gets or sets the dynamic array serializer.
        /// </summary>
        public static IBsonSerializer DynamicArraySerializer
        {
            get
            {
                if (!__dynamicArraySerializerWasSet)
                {
                    __dynamicArraySerializer = BsonSerializer.LookupSerializer<List<object>>();
                }
                return __dynamicArraySerializer;
            }
            set
            {
                __dynamicArraySerializerWasSet = true;
                __dynamicArraySerializer = value;
            }
        }

        /// <summary>
        /// Gets or sets the dynamic document serializer.
        /// </summary>
        public static IBsonSerializer DynamicDocumentSerializer
        {
            get
            {
                if (!__dynamicDocumentSerializerWasSet)
                {
                    __dynamicDocumentSerializer = BsonSerializer.LookupSerializer<ExpandoObject>();
                }
                return __dynamicDocumentSerializer;
            }
            set
            {
                __dynamicDocumentSerializerWasSet = true;
                __dynamicDocumentSerializer = value;
            }
        }

        /// <summary>
        /// Gets or sets the default representation to be used in serialization of 
        /// Guids to the database. 
        /// <seealso cref="SAEA.Mongo.Bson.GuidRepresentation"/> 
        /// </summary>
        public static GuidRepresentation GuidRepresentation
        {
            get { return __guidRepresentation; }
            set { __guidRepresentation = value; }
        }

        /// <summary>
        /// Gets or sets the default max document size. The default is 4MiB.
        /// </summary>
        public static int MaxDocumentSize
        {
            get { return __maxDocumentSize; }
            set { __maxDocumentSize = value; }
        }

        /// <summary>
        /// Gets or sets the default max serialization depth (used to detect circular references during serialization). The default is 100.
        /// </summary>
        public static int MaxSerializationDepth
        {
            get { return __maxSerializationDepth; }
            set { __maxSerializationDepth = value; }
        }
    }
}
