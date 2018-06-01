/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.WebAPI.Http.Base
*文件名： BaseHeader
*版本号： V1.0.0.0
*唯一标识：2ab45db1-f3f1-4ed5-99c4-5d11f52c8a6b
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/8 16:30:45
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/8 16:30:45
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.Commom;
using System;
using System.Text;

namespace SAEA.WebAPI.Http.Base
{
    public class BaseHeader
    {
        public const string ENTER = "\r\n";

        public const string DENTER = "\r\n\r\n";

        public const string SPACE = " ";

        public string Protocols { get; set; } = "HTTP/1.1";

        public byte[] Body { get; set; }        

        public Encoding Encoding { get; set; } = Encoding.UTF8;

        public string Content_Type { get; set; } = "application/json; charset=UTF-8";

        public string Content_Length { get; set; }

        public string Content_Encoding { get; set; }

        public string ContentLanguage { get; set; }

        public NameValueCollection Headers { get; set; } = new NameValueCollection();
        
        protected string GetHeader(Enum header)
        {
            var fieldName = header.GetDescription();
            if (fieldName == null) return null;
            var hasKey = Headers.ContainsName(fieldName);
            if (!hasKey) return null;
            return Headers[fieldName];
        }
        
        protected void SetHeader(Enum header, string value)
        {
            var fieldName = header.GetDescription();
            if (fieldName == null) return;
            var hasKey = Headers.ContainsName(fieldName);
            if (!hasKey) Headers.Add(fieldName, value);
            Headers[fieldName] = value;
        }
    }
}
