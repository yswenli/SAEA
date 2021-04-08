/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Commom
*文件名： HttpClientFactory
*版本号： v6.0.0.1
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2017/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2017/3/1 15:54:21
*修改人： yswenli
*版本号： v6.0.0.1
*描述：
*
*****************************************************************************/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Common.Caching;
using SAEA.Common.Serialization;

namespace SAEA.Common
{
    public static class HttpClientFactory
    {
        static ConcurrentBag<string> _pool;

        static MemoryCache<HttpClient> _cache;

        static HttpClientFactory()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3
                | SecurityProtocolType.Tls
                | SecurityProtocolType.Tls11
                | SecurityProtocolType.Tls12;

            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(CheckValidationResult);

            _pool = new ConcurrentBag<string>();

            _cache = new MemoryCache<HttpClient>();

            _cache.OnChanged += _cache_OnChanged;
        }

        private static void _cache_OnChanged(bool arg1, HttpClient arg2)
        {
            if (!arg1 && arg2 != null)
            {
                arg2.Dispose();
            }
        }

        static bool CheckValidationResult(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
        {
            return true;
        }


        static HttpClient GetClient()
        {
            HttpClient httpClient = null;

            if (_pool.TryTake(out string key))
            {
                httpClient = _cache.Get(key);
            }

            if (httpClient == null)
            {
                httpClient = new HttpClient();
                key = httpClient.GetHashCode().ToString();
                _cache.Set(key, httpClient, TimeSpan.FromDays(1));
            }
            else
            {
                _cache.Active(key, TimeSpan.FromDays(1));
            }
            return httpClient;
        }



        static void Free(HttpClient httpClient)
        {
            if (httpClient != null)
            {
                var key = httpClient.GetHashCode().ToString();
                _pool.Add(key);
                _cache.Active(key, TimeSpan.FromMinutes(1));
            }
        }

        /// <summary>
        /// 基础的http请求
        /// </summary>
        /// <param name="httpRequestMessage"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static async Task<HttpResponseMessage> SendAsync(HttpRequestMessage httpRequestMessage, int timeOut = 60 * 1000)
        {
            using (CancellationTokenSource cts = new CancellationTokenSource(timeOut))
            {
                var httpClient = GetClient();
                try
                {
                    if (httpClient != null)
                    {
                        httpClient.Timeout = TimeSpan.FromMilliseconds(timeOut);

                        return await httpClient.SendAsync(httpRequestMessage, cts.Token);
                    }
                }
                finally
                {
                    Free(httpClient);
                }
                return null;
            }
        }


        /// <summary>
        /// get请求
        /// </summary>
        /// <param name="url"></param>
        /// <param name="headers"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static async Task<byte[]> GetAsync(string url, Dictionary<string, string> headers = null, int timeOut = 60 * 1000)
        {
            var reqMsg = new HttpRequestMessage(new HttpMethod("GET"), url);
            if (headers != null && headers.Any())
            {
                foreach (var item in headers)
                {
                    if (item.Key.IndexOf("Content-Type", StringComparison.OrdinalIgnoreCase) > -1)
                    {
                        //传入类型
                        if (!string.IsNullOrEmpty(item.Value))
                        {
                            var acceptArr = item.Value.Split(";", StringSplitOptions.RemoveEmptyEntries);
                            reqMsg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(acceptArr[0]));
                            if (acceptArr.Length > 1)
                            {
                                reqMsg.Headers.AcceptCharset.Add(new StringWithQualityHeaderValue(acceptArr[1].Split("=")[1]));
                            }
                        }
                    }
                    else if (item.Key.IndexOf("Authorization", StringComparison.OrdinalIgnoreCase) > -1 && !string.IsNullOrEmpty(item.Value) && item.Value.IndexOf(" ") > -1)
                    {
                        //basic bear等标准较验
                        var authArr = item.Value.Split(" ");
                        if (authArr.Length == 2)
                        {
                            reqMsg.Headers.Authorization = new AuthenticationHeaderValue(authArr[0], authArr[1]);
                        }
                    }
                    else
                    {
                        //自定义headers
                        reqMsg.Headers.Add(item.Key, item.Value);
                    }
                }
            }

            var resMsg = await SendAsync(reqMsg, timeOut);

            return await resMsg.Content.ReadAsByteArrayAsync();
        }


        /// <summary>
        /// get请求
        /// </summary>
        /// <param name="url"></param>
        /// <param name="headers"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static async Task<string> GetJsonAsync(string url, Dictionary<string, string> headers = null, int timeOut = 60 * 1000)
        {
            if (headers == null)
                headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!headers.ContainsKey("Content-Type"))
                headers.Add("Content-Type", "application/json; charset=UTF-8");

            var bytes = await GetAsync(url, headers, timeOut);
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// get请求
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="url"></param>
        /// <param name="headers"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static async Task<T> GetViewModelAsync<T>(string url, Dictionary<string, string> headers = null, int timeOut = 60 * 1000)
        {
            if (headers == null)
                headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!headers.ContainsKey("Content-Type"))
                headers.Add("Content-Type", "application/json; charset=UTF-8");

            var json = await GetJsonAsync(url, headers, timeOut);

            if (string.IsNullOrEmpty(json))
            {
                return default(T);
            }
            return SerializeHelper.Deserialize<T>(json);
        }

        /// <summary>
        /// post表单数据
        /// </summary>
        /// <param name="url"></param>
        /// <param name="formData"></param>
        /// <param name="headers"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static async Task<byte[]> PostFormAsync(string url, Dictionary<string, string> formData, Dictionary<string, string> headers = null, int timeOut = 60 * 1000)
        {
            var form = new FormUrlEncodedContent(formData.ToList());

            var reqMsg = new HttpRequestMessage(new HttpMethod("POST"), url);

            reqMsg.Content = form;

            if (headers != null && headers.Any())
            {
                foreach (var item in headers)
                {
                    if (item.Key.IndexOf("Content-Type", StringComparison.OrdinalIgnoreCase) > -1)
                    {
                        continue;
                    }
                    if (item.Key.IndexOf("Authorization", StringComparison.OrdinalIgnoreCase) > -1 && !string.IsNullOrEmpty(item.Value) && item.Value.IndexOf(" ") > -1)
                    {
                        //basic bear等标准较验
                        var authArr = item.Value.Split(" ");
                        if (authArr.Length == 2)
                        {
                            reqMsg.Headers.Authorization = new AuthenticationHeaderValue(authArr[0], authArr[1]);
                        }
                    }
                    else
                    {
                        //自定义headers
                        reqMsg.Headers.Add(item.Key, item.Value);
                    }
                }
            }
            var resMsg = await SendAsync(reqMsg, timeOut);

            return await resMsg.Content.ReadAsByteArrayAsync();
        }
        /// <summary>
        /// post文件
        /// </summary>
        /// <param name="url"></param>
        /// <param name="formData"></param>
        /// <param name="headers"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static async Task<string> PostFileAsync(string url, Dictionary<string, object> formData, Dictionary<string, string> headers = null, int timeOut = 60 * 1000)
        {
            var boundary = DateTimeHelper.Now.ToString("X");
            var form = new MultipartFormDataContent(boundary);
            foreach (var item in formData)
            {
                if (item.Value is byte[])
                {
                    form.Add(new ByteArrayContent((byte[])item.Value), "media", $"\"{item.Key}\"");
                }
                else
                {
                    form.Add(new StringContent(item.Value.ToString()), item.Key);
                }
            }
            var reqMsg = new HttpRequestMessage(new HttpMethod("POST"), url);

            reqMsg.Content = form;

            if (headers != null && headers.Any())
            {
                foreach (var item in headers)
                {
                    if (item.Key.IndexOf("Content-Type", StringComparison.OrdinalIgnoreCase) > -1)
                    {
                        continue;
                    }
                    if (item.Key.IndexOf("Authorization", StringComparison.OrdinalIgnoreCase) > -1 && !string.IsNullOrEmpty(item.Value) && item.Value.IndexOf(" ") > -1)
                    {
                        //basic bear等标准较验
                        var authArr = item.Value.Split(" ");
                        if (authArr.Length == 2)
                        {
                            reqMsg.Headers.Authorization = new AuthenticationHeaderValue(authArr[0], authArr[1]);
                        }
                    }
                    else
                    {
                        //自定义headers
                        reqMsg.Headers.Add(item.Key, item.Value);
                    }
                }
            }
            var resMsg = await SendAsync(reqMsg, timeOut);

            return await resMsg.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// post请求
        /// </summary>
        /// <param name="url"></param>
        /// <param name="postData"></param>
        /// <param name="headers"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static async Task<byte[]> PostAsync(string url, byte[] postData, Dictionary<string, string> headers = null, int timeOut = 60 * 1000)
        {
            var form = new ByteArrayContent(postData);

            var reqMsg = new HttpRequestMessage(new HttpMethod("POST"), url);

            reqMsg.Content = form;

            if (headers != null && headers.Any())
            {
                foreach (var item in headers)
                {
                    if (item.Key.IndexOf("Content-Type", StringComparison.OrdinalIgnoreCase) > -1)
                    {
                        //传入类型
                        if (!string.IsNullOrEmpty(item.Value))
                        {
                            var acceptArr = item.Value.Split(";", StringSplitOptions.RemoveEmptyEntries);
                            reqMsg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(acceptArr[0]));
                            if (acceptArr.Length > 1)
                            {
                                reqMsg.Headers.AcceptCharset.Add(new StringWithQualityHeaderValue(acceptArr[1].Split("=")[1]));
                            }
                        }
                    }
                    else if (item.Key.IndexOf("Authorization", StringComparison.OrdinalIgnoreCase) > -1 && !string.IsNullOrEmpty(item.Value) && item.Value.IndexOf(" ") > -1)
                    {
                        //basic bear等标准较验
                        var authArr = item.Value.Split(" ");
                        if (authArr.Length == 2)
                        {
                            reqMsg.Headers.Authorization = new AuthenticationHeaderValue(authArr[0], authArr[1]);
                        }
                    }
                    else
                    {
                        //自定义headers
                        reqMsg.Headers.Add(item.Key, item.Value);
                    }
                }
            }
            var resMsg = await SendAsync(reqMsg, timeOut);

            return await resMsg.Content.ReadAsByteArrayAsync();
        }

        /// <summary>
        /// 传入Json
        /// </summary>
        /// <param name="url"></param>
        /// <param name="json"></param>
        /// <param name="headers"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static async Task<string> PostJsonAsync(string url, string json, Dictionary<string, string> headers = null, int timeOut = 60 * 1000)
        {
            var form = new StringContent(json);

            var reqMsg = new HttpRequestMessage(new HttpMethod("POST"), url);

            reqMsg.Content = form;

            if (headers == null)
                headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!headers.ContainsKey("Content-Type"))
                headers.Add("Content-Type", "application/json; charset=UTF-8");

            foreach (var item in headers)
            {
                if (item.Key.IndexOf("Content-Type", StringComparison.OrdinalIgnoreCase) > -1)
                {
                    //传入类型
                    if (!string.IsNullOrEmpty(item.Value))
                    {
                        var acceptArr = item.Value.Split(";", StringSplitOptions.RemoveEmptyEntries);
                        reqMsg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(acceptArr[0]));
                        if (acceptArr.Length > 1)
                        {
                            reqMsg.Headers.AcceptCharset.Add(new StringWithQualityHeaderValue(acceptArr[1].Split("=")[1]));
                        }
                    }

                }
                else if (item.Key.IndexOf("Authorization", StringComparison.OrdinalIgnoreCase) > -1 && !string.IsNullOrEmpty(item.Value) && item.Value.IndexOf(" ") > -1)
                {
                    //basic bear等标准较验
                    var authArr = item.Value.Split(" ");
                    if (authArr.Length == 2)
                    {
                        reqMsg.Headers.Authorization = new AuthenticationHeaderValue(authArr[0], authArr[1]);
                    }
                }
                else
                {
                    //自定义headers
                    reqMsg.Headers.Add(item.Key, item.Value);
                }
            }

            var resMsg = await SendAsync(reqMsg, timeOut);

            return await resMsg.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// post请求
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="url"></param>
        /// <param name="postModel"></param>
        /// <param name="headers"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static async Task<T> PostViewModelAsync<T>(string url, object postModel, Dictionary<string, string> headers = null, int timeOut = 60 * 1000)
        {
            var postData = SerializeHelper.Serialize(postModel);

            if (headers == null)
                headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!headers.ContainsKey("Content-Type"))
                headers.Add("Content-Type", "application/json; charset=UTF-8");

            var json = await PostJsonAsync(url, postData, headers, timeOut);

            if (string.IsNullOrEmpty(json))
            {
                return default(T);
            }
            return SerializeHelper.Deserialize<T>(json);
        }



    }


}
