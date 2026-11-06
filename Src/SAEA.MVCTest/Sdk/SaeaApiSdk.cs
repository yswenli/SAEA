//
//此代码为SAEA.MVC.APISdkCodeGenerator于2021-01-12 13:16:28.512生成，请尽量不要修改
//
using SAEA.Common;
using System;

namespace SAEA.MVC.Tool.CodeGenerte
{
    /// <summary>
    /// 服务端 API 调用 SDK，按方法封装各接口的 GET/POST 请求
    /// </summary>
    public class SaeaApiSdk
    {
        string _url = "";

        /// <summary>
        /// 以服务端根地址初始化 SDK
        /// </summary>
        /// <param name="url">服务端根地址，需以 / 结尾，请求时直接拼接 api/ 路径</param>
        public SaeaApiSdk(string url)
        {
            _url = url;
        }
        /// <summary>
        /// 以 GET 方式调用 api/Ajax/Test
        /// </summary>
        /// <param name="str">str 参数，URL 编码后拼入查询串</param>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void AjaxTestGet(String str,Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Ajax/Test","str=" + SAEA.Http.HttpUtility.UrlEncode(str),"Get");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 POST 方式调用 api/Ajax/Test
        /// </summary>
        /// <param name="str">str 参数，URL 编码后作为表单内容提交</param>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void AjaxTestPost(String str,Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Ajax/Test","str=" + SAEA.Http.HttpUtility.UrlEncode(str),"Post");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 GET 方式调用 api/Ajax/Test2
        /// </summary>
        /// <param name="str">str 参数，URL 编码后拼入查询串</param>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void AjaxTest2Get(String str,Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Ajax/Test2","str=" + SAEA.Http.HttpUtility.UrlEncode(str),"Get");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 POST 方式调用 api/Ajax/Test2
        /// </summary>
        /// <param name="str">str 参数，URL 编码后作为表单内容提交</param>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void AjaxTest2Post(String str,Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Ajax/Test2","str=" + SAEA.Http.HttpUtility.UrlEncode(str),"Post");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 GET 方式调用 api/Ajax/Test3
        /// </summary>
        /// <param name="ID">用户 ID</param>
        /// <param name="UserName">登录名</param>
        /// <param name="NickName">昵称</param>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void AjaxTest3Get(Int32 ID,String UserName,String NickName,Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Ajax/Test3","ID=" + ID + "&" + "UserName=" + SAEA.Http.HttpUtility.UrlEncode(UserName) + "&" + "NickName=" + SAEA.Http.HttpUtility.UrlEncode(NickName),"Get");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 POST 方式调用 api/Ajax/Test3
        /// </summary>
        /// <param name="ID">用户 ID</param>
        /// <param name="UserName">登录名</param>
        /// <param name="NickName">昵称</param>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void AjaxTest3Post(Int32 ID,String UserName,String NickName,Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Ajax/Test3","ID=" + ID + "&" + "UserName=" + SAEA.Http.HttpUtility.UrlEncode(UserName) + "&" + "NickName=" + SAEA.Http.HttpUtility.UrlEncode(NickName),"Post");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 GET 方式调用 api/Asynchronous/Hello
        /// </summary>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void AsynchronousHelloGet(Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Asynchronous/Hello","","Get");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 POST 方式调用 api/Asynchronous/Hello
        /// </summary>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void AsynchronousHelloPost(Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Asynchronous/Hello","","Post");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 GET 方式调用 api/Asynchronous/Test
        /// </summary>
        /// <param name="id">测试标识</param>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void AsynchronousTestGet(String id,Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Asynchronous/Test","id=" + SAEA.Http.HttpUtility.UrlEncode(id),"Get");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 POST 方式调用 api/Asynchronous/Test
        /// </summary>
        /// <param name="id">测试标识</param>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void AsynchronousTestPost(String id,Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Asynchronous/Test","id=" + SAEA.Http.HttpUtility.UrlEncode(id),"Post");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 GET 方式调用 api/Error/Test1
        /// </summary>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void ErrorTest1Get(Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Error/Test1","","Get");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 POST 方式调用 api/Error/Test1
        /// </summary>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void ErrorTest1Post(Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Error/Test1","","Post");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 GET 方式调用 api/EventStream/SendNotice
        /// </summary>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void EventStreamSendNoticeGet(Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/EventStream/SendNotice","","Get");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 POST 方式调用 api/EventStream/SendNotice
        /// </summary>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void EventStreamSendNoticePost(Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/EventStream/SendNotice","","Post");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 GET 方式调用 api/File/Download
        /// </summary>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void FileDownloadGet(Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/File/Download","","Get");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 POST 方式调用 api/File/Download
        /// </summary>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void FileDownloadPost(Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/File/Download","","Post");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 GET 方式调用 api/File/DownloadBigData
        /// </summary>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void FileDownloadBigDataGet(Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/File/DownloadBigData","","Get");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 POST 方式调用 api/File/DownloadBigData
        /// </summary>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void FileDownloadBigDataPost(Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/File/DownloadBigData","","Post");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 GET 方式调用 api/File/Upload
        /// </summary>
        /// <param name="name">待上传文件的名称</param>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void FileUploadGet(String name,Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/File/Upload","name=" + SAEA.Http.HttpUtility.UrlEncode(name),"Get");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 POST 方式调用 api/File/Upload
        /// </summary>
        /// <param name="name">待上传文件的名称</param>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void FileUploadPost(String name,Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/File/Upload","name=" + SAEA.Http.HttpUtility.UrlEncode(name),"Post");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 GET 方式调用 api/Home/Get
        /// </summary>
        /// <param name="id">标识</param>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void HomeGetGet(String id,Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Home/Get","id=" + SAEA.Http.HttpUtility.UrlEncode(id),"Get");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 POST 方式调用 api/Home/Get
        /// </summary>
        /// <param name="id">标识</param>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void HomeGetPost(String id,Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Home/Get","id=" + SAEA.Http.HttpUtility.UrlEncode(id),"Post");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 GET 方式调用 api/Home/Show
        /// </summary>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void HomeShowGet(Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Home/Show","","Get");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 POST 方式调用 api/Home/Show
        /// </summary>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void HomeShowPost(Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Home/Show","","Post");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 GET 方式调用 api/Home/Update
        /// </summary>
        /// <param name="isFemale">性别标识</param>
        /// <param name="ID">用户 ID</param>
        /// <param name="UserName">登录名</param>
        /// <param name="NickName">昵称</param>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void HomeUpdateGet(String isFemale,Int32 ID,String UserName,String NickName,Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Home/Update","isFemale=" + SAEA.Http.HttpUtility.UrlEncode(isFemale) + "&" + "ID=" + ID + "&" + "UserName=" + SAEA.Http.HttpUtility.UrlEncode(UserName) + "&" + "NickName=" + SAEA.Http.HttpUtility.UrlEncode(NickName),"Get");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 POST 方式调用 api/Home/Update
        /// </summary>
        /// <param name="isFemale">性别标识</param>
        /// <param name="ID">用户 ID</param>
        /// <param name="UserName">登录名</param>
        /// <param name="NickName">昵称</param>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void HomeUpdatePost(String isFemale,Int32 ID,String UserName,String NickName,Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Home/Update","isFemale=" + SAEA.Http.HttpUtility.UrlEncode(isFemale) + "&" + "ID=" + ID + "&" + "UserName=" + SAEA.Http.HttpUtility.UrlEncode(UserName) + "&" + "NickName=" + SAEA.Http.HttpUtility.UrlEncode(NickName),"Post");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 GET 方式调用 api/Home/Set
        /// </summary>
        /// <param name="isFemale">性别标识</param>
        /// <param name="ID">用户 ID</param>
        /// <param name="UserName">登录名</param>
        /// <param name="NickName">昵称</param>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void HomeSetGet(String isFemale,Int32 ID,String UserName,String NickName,Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Home/Set","isFemale=" + SAEA.Http.HttpUtility.UrlEncode(isFemale) + "&" + "ID=" + ID + "&" + "UserName=" + SAEA.Http.HttpUtility.UrlEncode(UserName) + "&" + "NickName=" + SAEA.Http.HttpUtility.UrlEncode(NickName),"Get");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 POST 方式调用 api/Home/Set
        /// </summary>
        /// <param name="isFemale">性别标识</param>
        /// <param name="ID">用户 ID</param>
        /// <param name="UserName">登录名</param>
        /// <param name="NickName">昵称</param>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void HomeSetPost(String isFemale,Int32 ID,String UserName,String NickName,Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Home/Set","isFemale=" + SAEA.Http.HttpUtility.UrlEncode(isFemale) + "&" + "ID=" + ID + "&" + "UserName=" + SAEA.Http.HttpUtility.UrlEncode(UserName) + "&" + "NickName=" + SAEA.Http.HttpUtility.UrlEncode(NickName),"Post");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 GET 方式调用 api/Home/GetModels
        /// </summary>
        /// <param name="version">版本号</param>
        /// <param name="UID">用户 ID</param>
        /// <param name="Token">登录令牌</param>
        /// <param name="PageIndex">页码</param>
        /// <param name="PageSize">每页记录数</param>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void HomeGetModelsGet(String version,Int32 UID,String Token,Int32 PageIndex,Int32 PageSize,Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Home/GetModels","version=" + SAEA.Http.HttpUtility.UrlEncode(version) + "&" + "UID=" + UID + "&" + "Token=" + SAEA.Http.HttpUtility.UrlEncode(Token) + "&" + "PageIndex=" + PageIndex + "&" + "PageSize=" + PageSize,"Get");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 POST 方式调用 api/Home/GetModels
        /// </summary>
        /// <param name="version">版本号</param>
        /// <param name="UID">用户 ID</param>
        /// <param name="Token">登录令牌</param>
        /// <param name="PageIndex">页码</param>
        /// <param name="PageSize">每页记录数</param>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void HomeGetModelsPost(String version,Int32 UID,String Token,Int32 PageIndex,Int32 PageSize,Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Home/GetModels","version=" + SAEA.Http.HttpUtility.UrlEncode(version) + "&" + "UID=" + UID + "&" + "Token=" + SAEA.Http.HttpUtility.UrlEncode(Token) + "&" + "PageIndex=" + PageIndex + "&" + "PageSize=" + PageSize,"Post");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 GET 方式调用 api/Home/Test
        /// </summary>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void HomeTestGet(Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Home/Test","","Get");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 POST 方式调用 api/Home/Test
        /// </summary>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void HomeTestPost(Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Home/Test","","Post");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 GET 方式调用 api/SaeaSdkTest/GetList
        /// </summary>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void SaeaSdkTestGetListGet(Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/SaeaSdkTest/GetList","","Get");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 POST 方式调用 api/SaeaSdkTest/GetList
        /// </summary>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void SaeaSdkTestGetListPost(Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/SaeaSdkTest/GetList","","Post");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 GET 方式调用 api/Test/Get
        /// </summary>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void TestGetGet(Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Test/Get","","Get");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 POST 方式调用 api/Test/Get
        /// </summary>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void TestGetPost(Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Test/Get","","Post");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 POST 方式调用 api/Test/Other
        /// </summary>
        /// <param name="ID">用户 ID</param>
        /// <param name="UserName">登录名</param>
        /// <param name="NickName">昵称</param>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void TestOtherPost(Int32 ID,String UserName,String NickName,Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Test/Other","ID=" + ID + "&" + "UserName=" + SAEA.Http.HttpUtility.UrlEncode(UserName) + "&" + "NickName=" + SAEA.Http.HttpUtility.UrlEncode(NickName),"Post");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 GET 方式调用 api/Test/Other
        /// </summary>
        /// <param name="ID">用户 ID</param>
        /// <param name="UserName">登录名</param>
        /// <param name="NickName">昵称</param>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void TestOtherGet(Int32 ID,String UserName,String NickName,Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Test/Other","ID=" + ID + "&" + "UserName=" + SAEA.Http.HttpUtility.UrlEncode(UserName) + "&" + "NickName=" + SAEA.Http.HttpUtility.UrlEncode(NickName),"Get");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 GET 方式调用 api/Test/Timeout
        /// </summary>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void TestTimeoutGet(Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Test/Timeout","","Get");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }
        /// <summary>
        /// 以 POST 方式调用 api/Test/Timeout
        /// </summary>
        /// <param name="sucess">成功回调，参数为响应文本</param>
        /// <param name="error">失败回调，参数为捕获到的异常</param>
        public void TestTimeoutPost(Action<string> sucess, Action<Exception> error)
        {
            try
            {
                var txt = ApiHelper.Request($"{_url}api/Test/Timeout","","Post");
                sucess?.Invoke(txt);
            }
            catch (Exception ex)
            {
                error?.Invoke(ex);
            }
        }

    }
}
