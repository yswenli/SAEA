/****************************************************************************
*项目名称：SAEA.Mongo.Bson
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Mongo.Bson
*类 名 称：BsonExtensionMethods
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/5/22 11:09:59
*描述：
*=====================================================================
*修改时间：2019/5/22 11:09:59
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Mongo.Bson.IO;
using SAEA.Mongo.Bson.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SAEA.Mongo.Bson
{
    /// <summary>
    /// A static class containing BSON extension methods.
    /// </summary>
    public static class BsonExtensionMethods
    {
        /// <summary>
        /// Serializes an object to a BSON byte array.
        /// </summary>
        /// <typeparam name="TNominalType">The nominal type of the object.</typeparam>
        /// <param name="obj">The object.</param>
        /// <param name="serializer">The serializer.</param>
        /// <param name="writerSettings">The writer settings.</param>
        /// <param name="configurator">The serialization context configurator.</param>
        /// <param name="args">The serialization args.</param>
        /// <returns>A BSON byte array.</returns>
        public static byte[] ToBson<TNominalType>(
            this TNominalType obj,
            IBsonSerializer<TNominalType> serializer = null,
            BsonBinaryWriterSettings writerSettings = null,
            Action<BsonSerializationContext.Builder> configurator = null,
            BsonSerializationArgs args = default(BsonSerializationArgs)
            )
        {
            return ToBson(obj, typeof(TNominalType), writerSettings, serializer, configurator, args);
        }

        /// <summary>
        /// Serializes an object to a BSON byte array.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="nominalType">The nominal type of the object..</param>
        /// <param name="writerSettings">The writer settings.</param>
        /// <param name="serializer">The serializer.</param>
        /// <param name="configurator">The serialization context configurator.</param>
        /// <param name="args">The serialization args.</param>
        /// <returns>A BSON byte array.</returns>
        /// <exception cref="System.ArgumentNullException">nominalType</exception>
        /// <exception cref="System.ArgumentException">serializer</exception>
        public static byte[] ToBson(
            this object obj,
            Type nominalType,
            BsonBinaryWriterSettings writerSettings = null,
            IBsonSerializer serializer = null,
            Action<BsonSerializationContext.Builder> configurator = null,
            BsonSerializationArgs args = default(BsonSerializationArgs))
        {
            if (nominalType == null)
            {
                throw new ArgumentNullException("nominalType");
            }

            if (serializer == null)
            {
                serializer = BsonSerializer.LookupSerializer(nominalType);
            }
            if (serializer.ValueType != nominalType)
            {
                var message = string.Format("Serializer type {0} value type does not match document types {1}.", serializer.GetType().FullName, nominalType.FullName);
                throw new ArgumentException(message, "serializer");
            }

            using (var memoryStream = new MemoryStream())
            {
                using (var bsonWriter = new BsonBinaryWriter(memoryStream, writerSettings ?? BsonBinaryWriterSettings.Defaults))
                {
                    var context = BsonSerializationContext.CreateRoot(bsonWriter, configurator);
                    args.NominalType = nominalType;
                    serializer.Serialize(context, args, obj);
                }
                return memoryStream.ToArray();
            }
        }

        /// <summary>
        /// Serializes an object to a BsonDocument.
        /// </summary>
        /// <typeparam name="TNominalType">The nominal type of the object.</typeparam>
        /// <param name="obj">The object.</param>
        /// <param name="serializer">The serializer.</param>
        /// <param name="configurator">The serialization context configurator.</param>
        /// <param name="args">The serialization args.</param>
        /// <returns>A BsonDocument.</returns>
        public static BsonDocument ToBsonDocument<TNominalType>(
            this TNominalType obj,
            IBsonSerializer<TNominalType> serializer = null,
            Action<BsonSerializationContext.Builder> configurator = null,
            BsonSerializationArgs args = default(BsonSerializationArgs))
        {
            return ToBsonDocument(obj, typeof(TNominalType), serializer, configurator, args);
        }

        /// <summary>
        /// Serializes an object to a BsonDocument.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="nominalType">The nominal type of the object.</param>
        /// <param name="serializer">The serializer.</param>
        /// <param name="configurator">The serialization context configurator.</param>
        /// <param name="args">The serialization args.</param>
        /// <returns>A BsonDocument.</returns>
        /// <exception cref="System.ArgumentNullException">nominalType</exception>
        /// <exception cref="System.ArgumentException">serializer</exception>
        public static BsonDocument ToBsonDocument(
            this object obj,
            Type nominalType,
            IBsonSerializer serializer = null,
            Action<BsonSerializationContext.Builder> configurator = null,
            BsonSerializationArgs args = default(BsonSerializationArgs))
        {
            if (nominalType == null)
            {
                throw new ArgumentNullException("nominalType");
            }

            if (obj == null)
            {
                return null;
            }

            if (serializer == null)
            {
                var bsonDocument = obj as BsonDocument;
                if (bsonDocument != null)
                {
                    return bsonDocument; // it's already a BsonDocument
                }

                var convertibleToBsonDocument = obj as IConvertibleToBsonDocument;
                if (convertibleToBsonDocument != null)
                {
                    return convertibleToBsonDocument.ToBsonDocument(); // use the provided ToBsonDocument method
                }

                serializer = BsonSerializer.LookupSerializer(nominalType);
            }
            if (serializer.ValueType != nominalType)
            {
                var message = string.Format("Serializer type {0} value type does not match document types {1}.", serializer.GetType().FullName, nominalType.FullName);
                throw new ArgumentException(message, "serializer");
            }

            // otherwise serialize into a new BsonDocument
            var document = new BsonDocument();
            using (var bsonWriter = new BsonDocumentWriter(document))
            {
                var context = BsonSerializationContext.CreateRoot(bsonWriter, configurator);
                args.NominalType = nominalType;
                serializer.Serialize(context, args, obj);
            }
            return document;
        }

        /// <summary>
        /// Serializes an object to a JSON string.
        /// </summary>
        /// <typeparam name="TNominalType">The nominal type of the object.</typeparam>
        /// <param name="obj">The object.</param>
        /// <param name="writerSettings">The JsonWriter settings.</param>
        /// <param name="serializer">The serializer.</param>
        /// <param name="configurator">The serializastion context configurator.</param>
        /// <param name="args">The serialization args.</param>
        /// <returns>
        /// A JSON string.
        /// </returns>
        public static string ToJson<TNominalType>(
            this TNominalType obj,
            JsonWriterSettings writerSettings = null,
            IBsonSerializer<TNominalType> serializer = null,
            Action<BsonSerializationContext.Builder> configurator = null,
            BsonSerializationArgs args = default(BsonSerializationArgs))
        {
            return ToJson(obj, typeof(TNominalType), writerSettings, serializer, configurator, args);
        }

        /// <summary>
        /// Serializes an object to a JSON string.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="nominalType">The nominal type of the objectt.</param>
        /// <param name="writerSettings">The JsonWriter settings.</param>
        /// <param name="serializer">The serializer.</param>
        /// <param name="configurator">The serialization context configurator.</param>
        /// <param name="args">The serialization args.</param>
        /// <returns>
        /// A JSON string.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">nominalType</exception>
        /// <exception cref="System.ArgumentException">serializer</exception>
        public static string ToJson(
            this object obj,
            Type nominalType,
            JsonWriterSettings writerSettings = null,
            IBsonSerializer serializer = null,
            Action<BsonSerializationContext.Builder> configurator = null,
            BsonSerializationArgs args = default(BsonSerializationArgs))
        {
            if (nominalType == null)
            {
                throw new ArgumentNullException("nominalType");
            }

            if (serializer == null)
            {
                serializer = BsonSerializer.LookupSerializer(nominalType);
            }
            if (serializer.ValueType != nominalType)
            {
                var message = string.Format("Serializer type {0} value type does not match document types {1}.", serializer.GetType().FullName, nominalType.FullName);
                throw new ArgumentException(message, "serializer");
            }

            using (var stringWriter = new StringWriter())
            {
                using (var bsonWriter = new JsonWriter(stringWriter, writerSettings ?? JsonWriterSettings.Defaults))
                {
                    var context = BsonSerializationContext.CreateRoot(bsonWriter, configurator);
                    args.NominalType = nominalType;
                    serializer.Serialize(context, args, obj);
                }
                return stringWriter.ToString();
            }
        }
    }
}
