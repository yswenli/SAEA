/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.WebAPI
*文件名： HttpApplication
*版本号： V1.0.0.0
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
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.WebAPI.Http;
using SAEA.WebAPI.Mvc;
using System;

namespace SAEA.WebAPI
{
    public class MvcApplication
    {
        HttpServer httpServer;


        public MvcApplication()
        {
            httpServer = new HttpServer();
        }


        /// <summary>
        /// 启动服务
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
    }
}
