/****************************************************************************
*Copyright (c) 2018-2022yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MVC
*文件名： SAEAMvcApplication
*版本号： v7.0.0.1
*唯一标识：85030224-1d7f-4fc0-8e65-f4b6144c6a46
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/10 13:59:33
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/10 13:59:33
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using System;

using SAEA.Common;
using SAEA.Common.NameValue;
using SAEA.Http;

namespace SAEA.MVC
{
    /// <summary>
    /// SAEA.MVC应用程序
    /// </summary>
    public partial class SAEAMvcApplication
    {
        WebHost _webHost;

        internal AreaCollection AreaCollection { get; private set; } = new AreaCollection();

        /// <summary>
        /// 是否处于运行中
        /// </summary>
        public bool Running { get; set; } = false;

        /// <summary>
        /// 自定义异常事件
        /// </summary>
        public event ExceptionHandler OnException;

        /// <summary>
        /// 自定义http处理
        /// </summary>
        public event RequestDelegate OnRequestDelegate;

        /// <summary>
        /// 构建mvc容器
        /// </summary>
        /// <param name="mvcConfig"></param>
        public SAEAMvcApplication(SAEAMvcApplicationConfig mvcConfig) : this(mvcConfig.Root, mvcConfig.Port, mvcConfig.IsStaticsCached, mvcConfig.IsZiped, mvcConfig.BufferSize, mvcConfig.Count, mvcConfig.ControllerNameSpace, mvcConfig.IsDebug)
        {
            _webHost.WebConfig.HomePage = mvcConfig.DefaultPage;
        }

        /// <summary>
        /// 构建mvc容器
        /// </summary>
        /// <param name="root">根目录</param>
        /// <param name="port">监听端口</param>
        /// <param name="isStaticsCached">是否启用静态缓存</param>
        /// <param name="isZiped">是压启用内容压缩</param>
        /// <param name="bufferSize">http处理数据缓存大小</param>
        /// <param name="count">http连接数上限</param>
        /// <param name="controllerNameSpace">注册指定的Controlls空间名</param>
        /// <param name="isDebug">调试模式</param>
        public SAEAMvcApplication(string root = "wwwroot", int port = 28080, bool isStaticsCached = true, bool isZiped = false, int bufferSize = 1024 * 10, int count = 10000, string controllerNameSpace = "", bool isDebug = false)
        {
            try
            {
                if (string.IsNullOrEmpty(controllerNameSpace))
                    AreaCollection.RegistAll();
                else
                    AreaCollection.RegistAll(controllerNameSpace);
            }
            catch (Exception ex)
            {
                throw new Exception("当前代码无任何Controller或者不符合MVC 命名规范！ err:" + ex.Message);
            }

            _webHost = new WebHost(typeof(HttpContext), root, port, isStaticsCached, isZiped, bufferSize, count, 120 * 1000, isDebug);

            _webHost.RouteParam = AreaCollection.RouteTable;
        }


        /// <summary>
        /// 设置默认路由地址
        /// </summary>
        /// <param name="controllerName"></param>
        /// <param name="actionName"></param>
        public void SetDefault(string controllerName, string actionName)
        {
            _webHost.WebConfig.DefaultRoute = new NameValueItem() { Name = controllerName, Value = actionName };
        }

        /// <summary>
        /// 设置默认首页
        /// </summary>
        /// <param name="defaultPage"></param>
        public void SetDefault(string defaultPage)
        {
            _webHost.WebConfig.HomePage = "/" + defaultPage;
        }

        /// <summary>
        /// 设置禁止访问列表
        /// </summary>
        /// <param name="list"></param>
        public void SetForbiddenAccessList(params string[] list)
        {
            _webHost.WebConfig.SetForbiddenAccessList(list);
        }

        /// <summary>
        /// 启动MVC服务
        /// </summary>
        public void Start()
        {
            try
            {
                if (OnException != null)
                    _webHost.OnException += _webHost_OnException;

                if (OnRequestDelegate != null)
                    _webHost.OnRequestDelegate += _webHost_OnRequestDelegate;

                _webHost.Start();
                this.Running = true;
            }
            catch (Exception ex)
            {
                throw new Exception("当前端口已被其他程序占用，请更换端口再做尝试！ err:" + ex.Message);
            }
        }

        private void _webHost_OnRequestDelegate(Http.Model.IHttpContext context)
        {
            OnRequestDelegate?.Invoke(context);
        }

        private Http.Model.IHttpResult _webHost_OnException(Http.Model.IHttpContext httpContext, Exception ex)
        {
            return OnException?.Invoke(httpContext, ex);
        }

        /// <summary>
        /// 设置需要跨域的自定义headers
        /// </summary>
        /// <param name="headers"></param>
        public void SetCrossDomainHeaders(params string[] headers)
        {
            ConstHelper.SetCrossDomainHeaders(headers);
        }
        /// <summary>
        /// 设置需要跨域的自定义headers
        /// </summary>
        /// <param name="headers"></param>
        public void SetCrossDomainHeaders(string headers)
        {
            ConstHelper.SetCrossDomainHeaders(headers);
        }

        /// <summary>
        /// 重启
        /// </summary>
        public void Restart()
        {
            this.Stop();
            this.Start();
        }

        /// <summary>
        /// 停止
        /// </summary>
        public void Stop()
        {
            try
            {
                _webHost.Stop();
                this.Running = false;
            }
            catch (Exception ex)
            {
                throw new Exception("关闭SAEAMvcApplication err:" + ex.Message);
            }
        }
    }
}
