/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.MVC
*文件名： HttpApplication
*版本号： V2.1.5.0
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
*版本号： V2.1.5.0
*描述：
*
*****************************************************************************/
using SAEA.MVC.Http;
using SAEA.MVC.Mvc;
using System;

namespace SAEA.MVC
{
    /// <summary>
    /// SAEA.MVC.Mvc应用程序
    /// </summary>
    public class SAEAMvcApplication
    {
        HttpServer httpServer;

        /// <summary>
        /// 是否启用静态缓存
        /// </summary>
        internal static bool IsStaticsCached { get; set; }

        /// <summary>
        /// 是压启用内容压缩
        /// </summary>
        internal static bool IsZiped { get; set; }

        /// <summary>
        /// 构建mvc容器
        /// </summary>
        /// <param name="isStaticsCached">是否启用静态缓存</param>
        /// <param name="isZiped">是压启用内容压缩</param>
        /// <param name="bufferSize">http处理数据缓存大小</param>
        /// <param name="count">http连接数上限</param>
        public SAEAMvcApplication(bool isStaticsCached = true, bool isZiped = true, int bufferSize = 1024 * 100, int count = 10000)
        {
            IsStaticsCached = isStaticsCached;
            IsZiped = isZiped;

            httpServer = new HttpServer(bufferSize, count);
        }

        /// <summary>
        /// 设置默认路由地址
        /// </summary>
        /// <param name="controllerName"></param>
        /// <param name="actionName"></param>
        public void SetDefault(string controllerName, string actionName)
        {
            AreaCollection.SetDefault(controllerName, actionName);
        }

        /// <summary>
        /// 启动MVC服务
        /// </summary>
        public void Start(int port = 39654)
        {
            try
            {
                AreaCollection.RegistAll();
            }
            catch (Exception ex)
            {
                throw new Exception("当前代码无任何Controller或者不符合MVC 命名规范！ err:" + ex.Message);
            }
            try
            {
                httpServer.Start(port);
            }
            catch (Exception ex)
            {
                throw new Exception("当前端口已被其他程序占用，请更换端口再做尝试！ err:" + ex.Message);
            }
        }
        /// <summary>
        /// 启动MVC服务
        /// </summary>
        /// <param name="controllerNameSpace">分离式的controller</param>
        /// <param name="port"></param>
        public void Start(string controllerNameSpace, int port = 39654)
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
            try
            {
                httpServer.Start(port);
            }
            catch (Exception ex)
            {
                throw new Exception("当前端口已被其他程序占用，请更换端口再做尝试！ err:" + ex.Message);
            }
        }
    }
}
