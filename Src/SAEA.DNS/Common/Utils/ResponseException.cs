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
*命名空间：SAEA.DNS.Common.Utils
*文件名： ResponseException
*版本号： v26.4.23.1
*唯一标识：2c276ca3-9389-419b-840b-fa935c31977e
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
using SAEA.DNS.Protocol;
using System;

namespace SAEA.DNS.Common.Utils
{
    public class ResponseException : Exception
    {
        private static string Format(IResponse response)
        {
            return string.Format("Invalid response received with code {0}", response.ResponseCode);
        }

        public ResponseException() { }
        public ResponseException(string message) : base(message) { }
        public ResponseException(string message, Exception e) : base(message, e) { }

        public ResponseException(IResponse response) : this(response, Format(response)) { }

        public ResponseException(IResponse response, Exception e)
            : base(Format(response), e)
        {
            Response = response;
        }

        public ResponseException(IResponse response, string message)
            : base(message)
        {
            Response = response;
        }

        public IResponse Response
        {
            get;
            private set;
        }
    }
}
