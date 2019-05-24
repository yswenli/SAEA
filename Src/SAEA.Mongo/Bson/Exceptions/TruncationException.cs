/****************************************************************************
*项目名称：SAEA.Mongo.Bson.Exceptions
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Mongo.Bson.Exceptions
*类 名 称：TruncationException
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/5/22 11:18:44
*描述：
*=====================================================================
*修改时间：2019/5/22 11:18:44
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
    /// Represents a truncation exception.
    /// </summary>
#if NET452
    [Serializable]
#endif
    public class TruncationException : BsonException
    {
        // constructors
        /// <summary>
        /// Initializes a new instance of the TruncationException class.
        /// </summary>
        public TruncationException()
            : this("Truncation resulted in data loss.")
        {
        }

        /// <summary>
        /// Initializes a new instance of the TruncationException class.
        /// </summary>
        /// <param name="message">The error message.</param>
        public TruncationException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the TruncationException class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public TruncationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

#if NET452
        /// <summary>
        /// Initializes a new instance of the TruncationException class (this overload used by deserialization).
        /// </summary>
        /// <param name="info">The SerializationInfo.</param>
        /// <param name="context">The StreamingContext.</param>
        public TruncationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }
}
