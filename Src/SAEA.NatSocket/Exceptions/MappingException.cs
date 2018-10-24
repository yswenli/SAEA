/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.NatSocket
*文件名： Class1
*版本号： V2.2.2.0
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
*版本号： V2.2.2.0
*描述：
*
*****************************************************************************/
using System;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace SAEA.NatSocket.Exceptions
{
    [Serializable]
    public class MappingException : Exception
    {
        /// <summary>
        /// 
        /// </summary>
        public int ErrorCode { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public string ErrorText { get; private set; }

        #region Constructors

        internal MappingException()
        {
        }

        internal MappingException(string message)
            : base(message)
        {
        }

        internal MappingException(int errorCode, string errorText)
            : base(string.Format("Error {0}: {1}", errorCode, errorText))
        {
            ErrorCode = errorCode;
            ErrorText = errorText;
        }

        internal MappingException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected MappingException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        #endregion

        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException("info");

            ErrorCode = info.GetInt32("errorCode");
            ErrorText = info.GetString("errorText");
            base.GetObjectData(info, context);
        }
    }
}
