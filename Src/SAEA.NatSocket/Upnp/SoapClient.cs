/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.NatSocket
*文件名： Class1
*版本号： v4.3.3.7
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
*版本号： v4.3.3.7
*描述：
*
*****************************************************************************/
using SAEA.NatSocket.Base;
using SAEA.NatSocket.Exceptions;
using SAEA.NatSocket.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace SAEA.NatSocket.Upnp
{
    internal class SoapClient
    {
        private readonly string _serviceType;
        private readonly Uri _url;

        public SoapClient(Uri url, string serviceType)
        {
            _url = url;
            _serviceType = serviceType;
        }


        public async Task<XmlDocument> InvokeAsync(string operationName, IDictionary<string, object> args)
        {
            NatDiscoverer.TraceSource.TraceEvent(TraceEventType.Verbose, 0, "SOAPACTION: **{0}** url:{1}", operationName,
                                                 _url);
            byte[] messageBody = BuildMessageBody(operationName, args);
            HttpWebRequest request = BuildHttpWebRequest(operationName, messageBody);

            if (messageBody.Length > 0)
            {
                using (var stream = await request.GetRequestStreamAsync())
                {
                    await stream.WriteAsync(messageBody, 0, messageBody.Length);
                }
            }

            using (var response = await GetWebResponse(request))
            {
                var stream = response.GetResponseStream();
                var contentLength = response.ContentLength;

                var reader = new StreamReader(stream, Encoding.UTF8);

                var responseBody = contentLength != -1
                                    ? reader.ReadAsMany((int)contentLength)
                                    : reader.ReadToEnd();

                var responseXml = GetXmlDocument(responseBody);

                response.Close();
                return responseXml;
            }
        }

        private static async Task<WebResponse> GetWebResponse(WebRequest request)
        {
            WebResponse response;
            try
            {
                response = await request.GetResponseAsync();
            }
            catch (WebException ex)
            {
                NatDiscoverer.TraceSource.TraceEvent(TraceEventType.Verbose, 0, "WebException status: {0}", ex.Status);

                // Even if the request "failed" we need to continue reading the response from the router
                response = ex.Response as HttpWebResponse;

                if (response == null)
                    throw;
            }
            return response;
        }


        private HttpWebRequest BuildHttpWebRequest(string operationName, byte[] messageBody)
        {

            var request = WebRequest.CreateHttp(_url);

            request.KeepAlive = false;
            request.Method = "POST";
            request.ContentType = "text/xml; charset=\"utf-8\"";
            request.Headers.Add("SOAPACTION", "\"" + _serviceType + "#" + operationName + "\"");
            request.ContentLength = messageBody.Length;
            return request;
        }

        private byte[] BuildMessageBody(string operationName, IEnumerable<KeyValuePair<string, object>> args)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<s:Envelope ");
            sb.AppendLine("   xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" ");
            sb.AppendLine("   s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">");
            sb.AppendLine("   <s:Body>");
            sb.AppendLine("	  <u:" + operationName + " xmlns:u=\"" + _serviceType + "\">");
            foreach (var a in args)
            {
                sb.AppendLine("		 <" + a.Key + ">" + Convert.ToString(a.Value, CultureInfo.InvariantCulture) +
                              "</" + a.Key + ">");
            }
            sb.AppendLine("	  </u:" + operationName + ">");
            sb.AppendLine("   </s:Body>");
            sb.Append("</s:Envelope>\r\n\r\n");
            string requestBody = sb.ToString();

            byte[] messageBody = Encoding.UTF8.GetBytes(requestBody);
            return messageBody;
        }

        private XmlDocument GetXmlDocument(string response)
        {
            XmlNode node;
            var doc = new XmlDocument();
            doc.LoadXml(response);

            var nsm = new XmlNamespaceManager(doc.NameTable);

            // Error messages should be found under this namespace
            nsm.AddNamespace("errorNs", "urn:schemas-upnp-org:control-1-0");

            // Check to see if we have a fault code message.
            if ((node = doc.SelectSingleNode("//errorNs:UPnPError", nsm)) != null)
            {
                int code = Convert.ToInt32(node.GetXmlElementText("errorCode"), CultureInfo.InvariantCulture);
                string errorMessage = node.GetXmlElementText("errorDescription");
                NatDiscoverer.TraceSource.LogWarn("Server failed with error: {0} - {1}", code, errorMessage);
                throw new MappingException(code, errorMessage);
            }

            return doc;
        }
    }
}
