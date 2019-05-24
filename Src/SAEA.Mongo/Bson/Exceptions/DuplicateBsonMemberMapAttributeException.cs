/****************************************************************************
*项目名称：SAEA.Mongo.Bson.Exceptions
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Mongo.Bson.Exceptions
*类 名 称：DuplicateBsonMemberMapAttributeException
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/5/22 11:18:25
*描述：
*=====================================================================
*修改时间：2019/5/22 11:18:25
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
    /// Indicates that an attribute restricted to one member has been applied to multiple members.
    /// </summary>
#if NET452
    [Serializable]
#endif
    public class DuplicateBsonMemberMapAttributeException : BsonException
    {
        // constructors 
        /// <summary>
        /// Initializes a new instance of the <see cref="DuplicateBsonMemberMapAttributeException" /> class.
        /// </summary>
        /// <param name="message">The message.</param>
        public DuplicateBsonMemberMapAttributeException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DuplicateBsonMemberMapAttributeException" /> class.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="inner">The inner.</param>
        public DuplicateBsonMemberMapAttributeException(string message, Exception inner)
            : base(message, inner)
        {
        }

#if NET452
        /// <summary>
        /// Initializes a new instance of the <see cref="DuplicateBsonMemberMapAttributeException" /> class.
        /// </summary>
        /// <param name="info">The info.</param>
        /// <param name="context">The context.</param>
        protected DuplicateBsonMemberMapAttributeException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }
}
