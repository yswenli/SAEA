/****************************************************************************
*项目名称：SAEA.Mongo.Bson.Exceptions
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Mongo.Bson.Exceptions
*类 名 称：BsonInternalException
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/5/22 11:17:13
*描述：
*=====================================================================
*修改时间：2019/5/22 11:17:13
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.Mongo.Bson.Exceptions
{
    /// <summary>
    /// Represents a BSON internal exception (almost surely the result of a bug).
    /// </summary>
#if NET452
    [Serializable]
#endif
    public class BsonInternalException : BsonException
    {
        // constructors
        /// <summary>
        /// Initializes a new instance of the BsonInternalException class.
        /// </summary>
        public BsonInternalException()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the BsonInternalException class.
        /// </summary>
        /// <param name="message">The error message.</param>
        public BsonInternalException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the BsonInternalException class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public BsonInternalException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

#if NET452
        /// <summary>
        /// Initializes a new instance of the BsonInternalException class (this overload used by deserialization).
        /// </summary>
        /// <param name="info">The SerializationInfo.</param>
        /// <param name="context">The StreamingContext.</param>
        public BsonInternalException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }
}
