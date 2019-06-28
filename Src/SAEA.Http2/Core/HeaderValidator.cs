/****************************************************************************
*项目名称：SAEA.Http2.Core
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Core
*类 名 称：HeaderValidator
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 16:10:29
*描述：
*=====================================================================
*修改时间：2019/6/27 16:10:29
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Http2.Model;
using System.Collections.Generic;
using System.Linq;

namespace SAEA.Http2.Core
{
    internal static class HeaderValidator
    {
        /// <summary>
        /// 验证HTTP/2请求的HeaderFields
        /// </summary>
        public static HeaderValidationResult ValidateRequestHeaders(
            IEnumerable<HeaderField> headerFields)
        {

            int nrMethod = 0;
            int nrScheme = 0;
            int nrAuthority = 0;
            int nrPath = 0;
            int nrPseudoFields = 0;

            foreach (var hf in headerFields)
            {
                if (hf.Name == null || hf.Name.Length == 0)
                {
                    return HeaderValidationResult.ErrorInvalidHeaderFieldName;
                }


                if (hf.Name[0] != ':') break;

                switch (hf.Name)
                {
                    case ":method":
                        nrMethod++;
                        break;
                    case ":path":
                        nrPath++;
                        break;
                    case ":authority":
                        nrAuthority++;
                        break;
                    case ":scheme":
                        nrScheme++;
                        break;
                    default:
                        return HeaderValidationResult.ErrorInvalidPseudoHeader;
                }

                if (hf.Value == null || hf.Value.Length < 1)
                {
                    return HeaderValidationResult.ErrorInvalidPseudoHeaderFieldValue;
                }
                nrPseudoFields++;
            }


            if (nrMethod != 1 || nrScheme != 1 || nrPath != 1 || (nrAuthority > 1))
            {
                return HeaderValidationResult.ErrorInvalidPseudoHeader;
            }

            return ValidateNormalHeaders(headerFields.Skip(nrPseudoFields));
        }

        /// <summary>
        /// 验证HTTP/2请求的HeaderFields
        /// </summary>
        public static HeaderValidationResult ValidateResponseHeaders(
            IEnumerable<HeaderField> headerFields)
        {
            int nrStatus = 0;
            int nrPseudoFields = 0;

            foreach (var hf in headerFields)
            {
                if (hf.Name == null || hf.Name.Length == 0)
                {
                    return HeaderValidationResult.ErrorInvalidHeaderFieldName;
                }

                if (hf.Name[0] != ':') break;

                switch (hf.Name)
                {
                    case ":status":
                        nrStatus++;

                        if (hf.Value == null ||
                            hf.Value.Length != 3 ||
                            hf.Value.Any(c => !char.IsNumber(c)))
                        {
                            return HeaderValidationResult.ErrorInvalidPseudoHeaderFieldValue;
                        }
                        break;
                    default:
                        return HeaderValidationResult.ErrorInvalidPseudoHeader;
                }

                if (hf.Value == null || hf.Value.Length < 1)
                {
                    return HeaderValidationResult.ErrorInvalidPseudoHeaderFieldValue;
                }
                nrPseudoFields++;
            }

            if (nrStatus != 1)
            {
                return HeaderValidationResult.ErrorInvalidPseudoHeader;
            }

            return ValidateNormalHeaders(headerFields.Skip(nrPseudoFields));
        }

        /// <summary>
        /// 验证HTTP/2请求的HeaderFields
        /// </summary>
        public static HeaderValidationResult ValidateTrailingHeaders(
            IEnumerable<HeaderField> headerFields)
        {
            return ValidateNormalHeaders(headerFields);
        }

        /// <summary>
        /// 验证非伪头列表。
        /// </summary>
        public static HeaderValidationResult ValidateNormalHeaders(
            IEnumerable<HeaderField> headerFields)
        {
            foreach (var hf in headerFields)
            {
                if (hf.Name == null || hf.Name.Length == 0)
                {
                    return HeaderValidationResult.ErrorInvalidHeaderFieldName;
                }

                if (hf.Name[0] == ':')
                {
                    return HeaderValidationResult.ErrorInvalidPseudoHeader;
                }

                if (hf.Name.Any(char.IsUpper))
                {
                    return HeaderValidationResult.ErrorInvalidFieldNameCase;
                }

                if (hf.Value == null)
                {
                    return HeaderValidationResult.ErrorInvalidHeaderFieldValue;
                }

                if (hf.Name == "connection")
                {
                    return HeaderValidationResult.ErrorInvalidConnectionHeader;
                }
                if (hf.Name == "te" && hf.Value != "trailers")
                {
                    return HeaderValidationResult.ErrorInvalidConnectionHeader;
                }
            }

            return HeaderValidationResult.Ok;
        }
    }
}
