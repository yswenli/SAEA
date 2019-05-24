/****************************************************************************
*项目名称：SAEA.Mongo.Bson.Exceptions
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Mongo.Bson.Exceptions
*类 名 称：BsonException
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/5/22 11:11:45
*描述：
*=====================================================================
*修改时间：2019/5/22 11:11:45
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.Mongo.Bson.Exceptions
{
#if NET452
    [Serializable]
#endif
    public class BsonException : Exception
    {
        // constructors
        /// <summary>
        /// Initializes a new instance of the BsonException class.
        /// </summary>
        public BsonException()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the BsonException class.
        /// </summary>
        /// <param name="message">The error message.</param>
        public BsonException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the BsonException class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public BsonException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the BsonException class.
        /// </summary>
        /// <param name="format">The error message format string.</param>
        /// <param name="args">One or more args for the error message.</param>
        public BsonException(string format, params object[] args)
            : base(string.Format(format, args))
        {
        }

#if NET452
        /// <summary>
        /// Initializes a new instance of the BsonException class (this overload used by deserialization).
        /// </summary>
        /// <param name="info">The SerializationInfo.</param>
        /// <param name="context">The StreamingContext.</param>
        public BsonException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }
}
