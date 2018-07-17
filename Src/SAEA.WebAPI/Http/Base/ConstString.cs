using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.WebAPI.Http.Base
{
    internal struct ConstString
    {
        public const string GETStr = "GET";

        public const string POSTStr = "POST";



        /// <summary>
        /// application/x-www-form-urlencoded
        /// </summary>
        public const string FORMENCTYPE1 = "application/x-www-form-urlencoded";

        /// <summary>
        /// multipart/form-data
        /// </summary>
        public const string FORMENCTYPE2 = "multipart/form-data";

        /// <summary>
        /// text/plain
        /// </summary>
        public const string FORMENCTYPE3 = "text/plain";
    }
}
