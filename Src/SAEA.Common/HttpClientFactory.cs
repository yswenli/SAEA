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
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SAEA.Common.Serialization;

namespace SAEA.Common
{
    public static class HttpClientFactory
    {
        static ConcurrentBag<HttpClient> _pool;

        static readonly int _total = 100;

        static int _size = 0;

        static HttpClientFactory()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3
                | SecurityProtocolType.Tls;
            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(CheckValidationResult);


            _pool = new ConcurrentBag<HttpClient>();

            for (int i = 0; i < 4; i++)
            {
                var httpClient = new HttpClient();
                _pool.Add(httpClient);
                Interlocked.Add(ref _size, 1);
            }
        }

        static bool CheckValidationResult(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
        {
            return true;
        }


        static HttpClient GetClient(CancellationToken token)
        {
            if (_pool.TryTake(out HttpClient httpClient))
            {
                return httpClient;
            }
            else
            {
                while (!token.IsCancellationRequested)
                {
                    if (_size < _total)
                    {
                        Interlocked.Add(ref _size, 1);
                        return new HttpClient();
                    }
                }
                return null;
            }
        }

        static void Free(HttpClient httpClient)
        {
            if (httpClient != null)
            {
                Interlocked.Add(ref _size, -1);
                _pool.Add(httpClient);
            }
        }


        /// <summary>
        /// get请求
        /// </summary>
        /// <param name="url"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static async Task<string> GetAsync(string url, int timeOut = 60 * 1000)
        {
            using (CancellationTokenSource cts = new CancellationTokenSource(timeOut))
            {
                var httpClient = GetClient(cts.Token);
                try
                {
                    if (httpClient != null)
                    {
                        httpClient.Timeout = TimeSpan.FromMilliseconds(timeOut);
                        httpClient.DefaultRequestHeaders.AcceptCharset.Add(new StringWithQualityHeaderValue("utf-8"));
                        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                        HttpResponseMessage response = await httpClient.GetAsync(url);
                        if (response.IsSuccessStatusCode)
                        {
                            return await response.Content.ReadAsStringAsync();
                        }
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
        /// <typeparam name="T"></typeparam>
        /// <param name="url"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static async Task<T> GetAsync<T>(string url, int timeOut = 60 * 1000)
        {
            var json = await GetAsync(url, timeOut);
            if (string.IsNullOrEmpty(json))
            {
                return default(T);
            }
            return SerializeHelper.Deserialize<T>(json);
        }

        /// <summary>
        /// post请求
        /// </summary>
        /// <param name="url"></param>
        /// <param name="postData"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static async Task<string> PostAsync(string url, string postData, int timeOut = 60 * 1000)
        {
            using (CancellationTokenSource cts = new CancellationTokenSource(timeOut))
            {
                var httpClient = GetClient(cts.Token);
                try
                {
                    httpClient.Timeout = TimeSpan.FromMilliseconds(timeOut);
                    HttpContent httpContent = new StringContent(postData);
                    httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    httpContent.Headers.ContentType.CharSet = "utf-8";

                    HttpResponseMessage response = await httpClient.PostAsync(url, httpContent);
                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsStringAsync();
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
        /// post请求
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="url"></param>
        /// <param name="postData"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static async Task<T> PostAsync<T>(string url, string postData, int timeOut = 60 * 1000)
        {
            var json = await PostAsync(url, postData, timeOut);
            if (string.IsNullOrEmpty(json))
            {
                return default(T);
            }
            return SerializeHelper.Deserialize<T>(json);
        }


    }


}
