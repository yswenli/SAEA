using System;

namespace SAEA.P2P.Common
{
    public class P2PException : Exception
    {
        public string ErrorCode { get; }

        public P2PException(string errorCode) : base(Common.ErrorCode.GetDescription(errorCode))
        {
            ErrorCode = errorCode;
        }

        public P2PException(string errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }

        public P2PException(string errorCode, string message, Exception innerException) 
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }
}