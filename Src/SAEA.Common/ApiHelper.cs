/****************************************************************************
*项目名称：SAEA.Common
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Common
*类 名 称：ApiHelper
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/24 14:26:12
*描述：
*=====================================================================
*修改时间：2020/12/24 14:26:12
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Common.Newtonsoft.Json;
using SAEA.Common.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Common
{
    /// <summary>
    /// api工具类日志委托
    /// </summary>
    /// <param name="log"></param>
    public delegate void ApiLogHandler(APILog log);

    /// <summary>
    /// api工具类
    /// </summary>
    public class ApiHelper : WebClient
    {
        int _timeOut = 180 * 1000;

        /// <summary>
        /// 自定义webclient
        /// </summary>
        /// <param name="timeOut"></param>
        public ApiHelper(int timeOut = 180 * 1000)
        {
            _timeOut = timeOut;

            ServicePointManager.DefaultConnectionLimit = 1024;
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3
                | SecurityProtocolType.Tls;
            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(CheckValidationResult);
        }
        #region ssl
        /// <summary>
        /// 服务器免密验证
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="certificate"></param>
        /// <param name="chain"></param>
        /// <param name="errors"></param>
        /// <returns></returns>
        public static bool CheckValidationResult(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
        {
            return true;
        }
        #endregion
        /// <summary>
        /// 重写后支持自解压
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        protected override WebRequest GetWebRequest(Uri address)
        {
            HttpWebRequest request = base.GetWebRequest(address) as HttpWebRequest;
            request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip | DecompressionMethods.None;
            request.Timeout = request.ReadWriteTimeout = _timeOut;
            request.AllowAutoRedirect = false;
            return request;
        }

        /// <summary>
        /// 扩展下载方法
        /// 增加显示下载进度
        /// </summary>
        /// <param name="url"></param>
        /// <param name="fileName"></param>
        /// <param name="process"></param>
        /// <param name="complete"></param>
        public void DownloadFileAsync(string url, string fileName, Action<long, long> process, Action<bool, Exception> complete)
        {
            Task.Factory.StartNew(() =>
            {
                var running = true;
                try
                {
                    HttpWebRequest request = (HttpWebRequest)GetWebRequest(new Uri(url));
                    request.Accept = @"text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
                    request.Referer = url;
                    request.UserAgent = @" Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/33.0.1750.154 Safari/537.36";
                    request.ContentType = "application/octet-stream";

                    HttpWebResponse response = request.GetResponse() as HttpWebResponse;
                    Stream stream = response.GetResponseStream();

                    long total = response.ContentLength;
                    long offset = 0;

                    Task.Factory.StartNew(() =>
                    {
                        while (running)
                        {
                            Thread.Sleep(1000);
                            if (process != null)
                                process.Invoke(total, offset);
                        }
                    });

                    using (FileStream fs = File.Create(fileName))
                    {
                        fs.Position = 0;

                        int num = 0;
                        do
                        {
                            byte[] buffer = new byte[1024];

                            num = stream.Read(buffer, 0, 1024);

                            fs.Write(buffer, 0, num);

                            offset += num;

                        } while (num > 0);

                        fs.Flush();
                    }

                    if (complete != null) complete.Invoke(true, null);
                }
                catch (Exception ex)
                {
                    if (complete != null) complete.Invoke(false, ex);
                }
                running = false;
            });
        }

        /// <summary>
        /// 扩展下载方法
        /// 增加显示下载进度
        /// </summary>
        /// <param name="url"></param>
        /// <param name="json"></param>
        /// <param name="fileName"></param>
        /// <param name="method"></param>
        /// <param name="headers"></param>
        /// <returns></returns>
        public bool DownloadFile(string url, string json, string fileName, string method = "GET", WebHeaderCollection headers = null)
        {
            try
            {
                var str = string.Empty;
                ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(CheckValidationResult);
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = method;

                if (headers != null)
                {
                    foreach (var key in headers.AllKeys)
                    {
                        if (key.ToLower() == "content-type")
                        {
                            request.ContentType = headers[key];
                            continue;
                        }

                        if (request.Headers.AllKeys.Contains(key))
                        {
                            request.Headers[key] = headers[key];
                        }
                        else
                        {
                            request.Headers.Add(key, headers[key]);
                        }
                    }
                }
                if (!string.IsNullOrEmpty(json))
                {
                    byte[] buffer = Encoding.UTF8.GetBytes(json);
                    request.ContentLength = buffer.Length;
                    request.GetRequestStream().Write(buffer, 0, buffer.Length);
                }

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                var stream = response.GetResponseStream();

                using (FileStream fs = File.Create(fileName))
                {
                    fs.Position = 0;
                    var offset = 0;
                    int num = 0;
                    do
                    {
                        byte[] data = new byte[1024];

                        num = stream.Read(data, 0, 1024);

                        fs.Write(data, 0, num);

                        offset += num;

                    } while (num > 0);

                    fs.Flush();
                }

                response.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("DownloadFile Error:" + ex.Message);
                return false;
            }

            return true;
        }


        /// <summary>
        /// 记录接口请求日志
        /// </summary>
        public static event ApiLogHandler OnLogged;


        /// <summary>
        /// 将实体发送给远程服务器
        /// 发送json
        /// </summary>
        /// <param name="url"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string Post(string url, Object obj)
        {
            return Post(url, SerializeHelper.Serialize(obj));
        }

        /// <summary>
        /// 将json发送给远程服务器
        /// </summary>
        /// <param name="url"></param>
        /// <param name="json"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static string Post(string url, string json, int timeOut)
        {
            var func = new Func<string>(() =>
            {
                using (ApiHelper client = new ApiHelper(timeOut))
                {
                    client.Encoding = Encoding.UTF8;
                    client.Headers.Add(HttpRequestHeader.Accept, "*/*");
                    client.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; SV1; .NET CLR 1.1.4322; .NET CLR 2.0.50727)");
                    client.Headers.Add("Content-Type", "application/json");
                    return client.UploadString(url, "POST", json);
                }
            });

            return TackingLog(false, func, url, json, timeOut);
        }
        /// <summary>
        /// post请求
        /// </summary>
        /// <param name="url"></param>
        /// <param name="postParam"></param>
        /// <param name="isSetUniqueId">是否设置唯一请求id</param>
        /// <returns></returns>
        public static string Post(string url, string postParam, bool isSetUniqueId)
        {
            WebHeaderCollection header = new WebHeaderCollection();
            if (isSetUniqueId)
            {
                var uniqueID = IdWorker.GetId().ToString();
                header.Add("uniqueid", uniqueID);
            }
            return Post(url, postParam, header);
        }

        /// <summary>
        /// 发送实体到服务器并返回实体
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="url"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        public static T2 Post<T1, T2>(string url, T1 t)
        {
            var json = Post(url, SerializeHelper.Serialize(t));
            if (string.IsNullOrEmpty(json))
            {
                return default(T2);
            }
            return SerializeHelper.Deserialize<T2>(json);
        }

        /// <summary>
        /// 上传文件
        /// </summary>
        /// <param name="url"></param>
        /// <param name="fileName"></param>
        /// <param name="headers"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static string UploadFile(string url, string fileName, WebHeaderCollection headers = null, int timeOut = 10 * 60 * 1000)
        {
            using (ApiHelper client = new ApiHelper(timeOut))
            {
                ServicePointManager.DefaultConnectionLimit = 512;
                client.Encoding = Encoding.UTF8;
                client.Headers.Add(HttpRequestHeader.Accept, "*/*");
                client.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; SV1; .NET CLR 1.1.4322; .NET CLR 2.0.50727)");
                //client.Headers.Add("Accept-Encoding", "gzip");
                //client.Headers.Add("ContentEncoding", "gzip");
                client.Headers.Add("Content-Type", "application/json");
                if (headers != null)
                {
                    foreach (var item in headers.AllKeys)
                    {
                        client.Headers.Add(item, headers[item]);
                    }
                }
                return Encoding.UTF8.GetString(client.UploadFile(url, "POST", fileName));
            }
        }

        /// <summary>
        /// 将json发送给远程服务器
        /// </summary>
        /// <param name="url"></param>
        /// <param name="json"></param>
        /// <param name="headers"></param>
        /// <returns></returns>
        public static string Post(string url, string json, WebHeaderCollection headers = null, int timeOut = 5 * 1000)
        {
            var func = new Func<string>(() =>
            {
                using (ApiHelper client = new ApiHelper(timeOut))
                {
                    client.Encoding = Encoding.UTF8;
                    client.Headers.Add(HttpRequestHeader.Accept, "*/*");
                    client.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; SV1; .NET CLR 1.1.4322; .NET CLR 2.0.50727)");
                    //client.Headers.Add("Accept-Encoding", "gzip");
                    //client.Headers.Add("ContentEncoding", "gzip");
                    client.Headers.Add("Content-Type", "application/json");
                    if (headers != null)
                    {
                        foreach (var item in headers.AllKeys)
                        {
                            client.Headers.Add(item, headers[item]);
                        }
                    }
                    return client.UploadString(url, "POST", json);
                }
            });

            return TackingLog(false, func, url, json, headers);
        }

        /// <summary>
        /// 带重试次数的post json方法
        /// </summary>
        /// <param name="url"></param>
        /// <param name="json"></param>
        /// <param name="tryTimes"></param>
        /// <param name="headers"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static string TryPost(string url, string json, int tryTimes = 10, WebHeaderCollection headers = null, int timeOut = 120 * 1000)
        {
            var func = new Func<string>(() =>
            {
                var fun = new Func<string>(() =>
                {
                    using (ApiHelper client = new ApiHelper(timeOut))
                    {
                        client.Encoding = Encoding.UTF8;
                        client.Headers.Add(HttpRequestHeader.Accept, "*/*");
                        client.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; SV1; .NET CLR 1.1.4322; .NET CLR 2.0.50727)");
                        client.Headers.Add("Content-Type", "application/json");
                        if (headers != null)
                        {
                            foreach (var item in headers.AllKeys)
                            {
                                client.Headers.Add(item, headers[item]);
                            }
                        }
                        return client.UploadString(url, "POST", json);
                    }
                });

                int times = 0;

                var result = string.Empty;

                while (true)
                {
                    try
                    {
                        times++;
                        return fun.Invoke();
                    }
                    catch (WebException wex)
                    {
                        if (wex.Status == WebExceptionStatus.Timeout)
                        {
                            throw wex;
                        }
                        else
                        {
                            if (times > tryTimes)
                            {
                                throw new Exception("已达到最大重试次数，Times:" + times + ",Error:" + wex.Message);
                            }
                            else
                            {
                                Thread.Sleep(times * 1000);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (times > tryTimes)
                        {
                            throw new Exception("已达到最大重试次数，Times:" + times + ",Error:" + ex.Message);
                        }
                        else
                        {
                            Thread.Sleep(times * 1000);
                        }
                    }
                }
            });

            return TackingLog(false, func, url, json, headers);
        }

        /// <summary>
        /// 带重试次数的post
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="url"></param>
        /// <param name="json"></param>
        /// <param name="tryTimes"></param>
        /// <param name="headers"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static T TryPost<T>(string url, string json, int tryTimes = 10, WebHeaderCollection headers = null, int timeOut = 120 * 1000)
        {
            return SerializeHelper.Deserialize<T>(TryPost(url, json, tryTimes, headers, timeOut));
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="url"></param>
        /// <param name="keyval"></param>
        /// <returns></returns>
        public static string PostKV(string url, string keyval)
        {
            var func = new Func<string>(() =>
            {
                using (ApiHelper client = new ApiHelper(5 * 1000))
                {
                    client.Encoding = Encoding.UTF8;
                    client.Headers.Add(HttpRequestHeader.Accept, "*/*");
                    client.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; SV1; .NET CLR 1.1.4322; .NET CLR 2.0.50727)");
                    //client.Headers.Add("Accept-Encoding", "gzip");
                    //client.Headers.Add("ContentEncoding", "gzip");
                    return client.UploadString(url, "POST", keyval);
                }
            });

            return TackingLog(false, func, url, keyval);
        }
        /// <summary>
        /// 上传数据到服务器
        /// </summary>
        /// <param name="url"></param>
        /// <param name="data"></param>
        /// <param name="headers"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static byte[] Post(string url, byte[] data, WebHeaderCollection headers = null)
        {
            var func = new Func<byte[]>(() =>
            {
                using (ApiHelper client = new ApiHelper(5 * 1000))
                {
                    client.Encoding = Encoding.UTF8;
                    client.Headers.Add(HttpRequestHeader.Accept, "*/*");
                    client.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; SV1; .NET CLR 1.1.4322; .NET CLR 2.0.50727)");
                    //client.Headers.Add("Accept-Encoding", "gzip");
                    //client.Headers.Add("ContentEncoding", "gzip");
                    client.Headers.Add("Content-Type", "application/json");
                    if (headers != null)
                    {
                        foreach (var item in headers.AllKeys)
                        {
                            client.Headers.Add(item, headers[item]);
                        }
                    }
                    return client.UploadData(url, "POST", data);
                }
            });

            return TackingLog(false, func, url, headers);
        }

        /// <summary>
        /// 获取远程服务器数据
        /// </summary>
        /// <param name="url"></param>
        /// <param name="headers"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static string Get(string url, WebHeaderCollection headers = null, int timeOut = 5 * 1000)
        {
            return TryGet(url, headers, timeOut);
        }

        /// <summary>
        /// 获取远程服务器数据
        /// </summary>
        /// <param name="url"></param>
        /// <param name="headers"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static string TryGet(string url, WebHeaderCollection headers = null, int timeOut = 120 * 1000)
        {
            var func = new Func<string>(() =>
            {
                using (ApiHelper client = new ApiHelper(timeOut))
                {
                    client.Encoding = Encoding.UTF8;
                    client.Headers.Add(HttpRequestHeader.Accept, "*/*");
                    client.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; SV1; .NET CLR 1.1.4322; .NET CLR 2.0.50727)");
                    //client.Headers.Add("Accept-Encoding", "gzip");
                    //client.Headers.Add("ContentEncoding", "gzip");
                    client.Headers.Add("Content-Type", "application/json");
                    if (headers != null)
                    {
                        foreach (var item in headers.AllKeys)
                        {
                            client.Headers.Add(item, headers[item]);
                        }
                    }
                    return client.DownloadString(url);
                }
            });

            return TackingLog(true, func, url, headers);
        }

        /// <summary>
        /// 获取远程服务器数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="url"></param>
        /// <param name="headers"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public static T TryGet<T>(string url, WebHeaderCollection headers = null, int timeOut = 120 * 1000)
        {
            var json = TryGet(url, headers, timeOut);
            if (!string.IsNullOrEmpty(json))
            {
                return SerializeHelper.Deserialize<T>(json);
            }
            return default(T);
        }


        /// <summary>
        /// 地址参数编码
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string UrlEcode(string data)
        {
            StringBuilder sb = new StringBuilder();
            byte[] byStr = Encoding.UTF8.GetBytes(data); //默认是Encoding.Default.GetBytes(str)
            for (int i = 0; i < byStr.Length; i++)
            {
                sb.Append(@"%" + Convert.ToString(byStr[i], 16));
            }
            return (sb.ToString());
        }


        /// <summary>
        /// 外部请求跟踪日志方法
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="get"></param>
        /// <param name="func"></param>
        /// <param name="params"></param>
        /// <returns>logcom 第一个参数为url，最后一个参数是返回值</returns>
        public static T TackingLog<T>(bool get, Func<T> func, params object[] @params)
        {
            if (@params == null)
            {
                return default(T);
            }

            var list = new List<object>();

            list.AddRange(@params);

            try
            {
                StackTrace st = new StackTrace();

                var sfs = st.GetFrames();

                for (int i = 1; i < sfs.Length; ++i)
                {
                    if (StackFrame.OFFSET_UNKNOWN == sfs[i].GetILOffset()) break;

                    var methodName = sfs[i].GetMethod().Name;

                    var fullName = sfs[i].GetFileName() + "()->" + methodName;

                    list.Add(fullName);
                }
            }
            catch { }

            T result = default(T);

            var mil = 0L;

            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                result = func.Invoke();
                mil = stopwatch.ElapsedMilliseconds;
                list.Add("cost:" + mil);
                list.Add(mil);
                list.Add(result);
            }
            catch (Exception ex)
            {
                mil = stopwatch.ElapsedMilliseconds;
                list.Add("cost:" + mil);
                list.Add(mil);
                list.Add("err:" + ex.Message);

                if (ex.Message.IndexOf("操作超时") > -1 || ex.Message.IndexOf("Timeout") > -1)
                {
                    OnLogged?.Invoke(new APILog(level: 2,
                        operationName: "调用五秒超时日志",
                        des: get ? "ApiHelper.Get" : "ApiHelper.Post",
                        url: @params[0].ToString(),
                        result: "",
                        cost: (int)mil,
                        method: (get ? "GET" : "POST"),
                        mode: "Active",
                        exp: ex,
                        @params: list.ToArray()));
                }
                else
                {
                    OnLogged?.Invoke(new APILog(level: 3,
                        operationName: "调用异常日志",
                        des: get ? "ApiHelper.Get" : "ApiHelper.Post",
                        url: @params[0].ToString(),
                        result: "",
                        cost: (int)mil,
                        method: (get ? "GET" : "POST"),
                        mode: "Active",
                        exp: ex,
                        @params: list.ToArray()));
                }
            }
            finally
            {
                OnLogged?.Invoke(new APILog(level: 0,
                       operationName: "调用日志",
                       des: get ? "ApiHelper.Get" : "ApiHelper.Post",
                       url: @params[0].ToString(),
                       result: SerializeHelper.Serialize(result),
                       cost: (int)mil,
                       method: (get ? "GET" : "POST"),
                       mode: "Active",
                       exp: null,
                       @params: list.ToArray()));

                stopwatch.Stop();
            }
            return result;
        }
    }


    /// <summary>
    /// APILog
    /// </summary>
    public class APILog
    {
        /// <summary>
        /// 流转唯一标识
        /// </summary>
        public string UniqueID
        {
            get; set;
        }

        /// <summary>
        /// 操作名称
        /// </summary>
        public string OperateName
        {
            get; set;
        }

        /// <summary>
        /// 日志描述
        /// </summary>
        public string Description
        {
            get; set;
        }

        /// <summary>
        /// 日志级别
        /// </summary>
        public int Level
        {
            get; set;
        }

        /// <summary>
        /// Url地址
        /// </summary>
        public string Url
        {
            get; set;
        }

        /// <summary>
        /// Get-Post
        /// </summary>
        public string Method
        {
            get; set;
        } = "GET";

        /// <summary>
        /// Active-Passive
        /// </summary>
        public string Mode
        {
            get; set;
        } = "Passive";

        /// <summary>
        /// 结果集
        /// </summary>
        public string Result
        {
            get; set;
        }

        /// <summary>
        /// 用时
        /// </summary>
        public int Cost
        {
            get; set;
        }

        /// <summary>
        /// 传入参数
        /// </summary>
        public string Params
        {
            get; set;
        }

        /// <summary>
        /// 异常消息
        /// </summary>
        public string ExceptionMsg
        {
            get; set;
        }
        /// <summary>
        /// 异常
        /// </summary>
        [JsonIgnore]
        public Exception Exception
        {
            get; set;
        }
        /// <summary>
        /// 调用者ip
        /// </summary>
        public string FromIP
        {
            get; set;
        }

        /// <summary>
        /// 记录时间
        /// </summary>
        [JsonProperty(Order = -99999)]
        public DateTime Created
        {
            get; set;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public APILog() { }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="level"></param>
        /// <param name="operationName"></param>
        /// <param name="des"></param>
        /// <param name="url"></param>
        /// <param name="result"></param>
        /// <param name="cost"></param>
        /// <param name="exp"></param>
        /// <param name="method"></param>
        /// <param name="mode"></param>
        /// <param name="params"></param>
        public APILog(int level, string operationName, string des, string url, string result, int cost, Exception exp, string method, string mode, params object[] @params)
        {
            UniqueID = IdWorker.GetId().ToString();
            Level = level;
            Created = DateTimeHelper.Now;
            OperateName = string.IsNullOrEmpty(operationName) ? "被调用日志" : operationName;
            Description = des;

            Url = GetUrlWithoutQuery(url);
            Method = string.IsNullOrEmpty(method) ? "GET" : method;
            Mode = string.IsNullOrEmpty(mode) ? "Passive" : mode;
            Result = result;
            Cost = cost;
            StringBuilder json = new StringBuilder();
            if (@params != null && @params.Any())
            {
                foreach (var param in @params)
                {
                    if (param == @params.First())
                    {
                        json.Append(SerializeHelper.Serialize(param));
                    }
                    else
                    {
                        json.Append($",{SerializeHelper.Serialize(param)}");
                    }
                }
            }
            Params = json.ToString();
            Exception = exp;
            ExceptionMsg = exp?.Message;
            FromIP = IPHelper.GetLocalIp();
        }

        /// <summary>
        /// 获取url不含query信息
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private string GetUrlWithoutQuery(string url)
        {
            if (string.IsNullOrEmpty(url))
                return string.Empty;

            try
            {
                var uri = new Uri(url);
                if (string.IsNullOrEmpty(uri.Query))
                {
                    return url;
                }
                var index = url.IndexOf(uri.AbsolutePath);
                if (index > 0)
                {
                    return url.Substring(0, index) + uri.AbsolutePath;
                }
            }
            catch
            {
            }

            return url;
        }

    }
}
