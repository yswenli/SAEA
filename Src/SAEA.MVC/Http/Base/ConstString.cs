namespace SAEA.WebAPI.Http.Base
{
    internal struct ConstString
    {
        public const string GETStr = "GET";

        public const string POSTStr = "POST";

        public const string OPTIONSStr = "OPTIONS";



        /// <summary>
        /// application/x-www-form-urlencoded
        /// </summary>
        public const string FORMENCTYPE1 = "application/x-www-form-urlencoded";

        /// <summary>
        /// multipart/form-data
        /// </summary>
        public const string FORMENCTYPE2 = "multipart/form-data";

        /// <summary>
        /// application/json
        /// </summary>
        public const string FORMENCTYPE3 = "application/json";        
    }
}
