/****************************************************************************
 * 
   ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http.Model
*文件名： WebConfig
*版本号： v26.4.23.1
*唯一标识：9cd40b0f-8d6c-4db1-b6a9-ab23418803a4
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/28 14:19:50
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/10/28 14:19:50
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System.Collections.Generic;

using SAEA.Common.NameValue;

namespace SAEA.Http.Model
{
    /// <summary>
    /// WebServerConfig
    /// </summary>
    public class WebConfig
    {
        /// <summary>
        /// 监听端口
        /// </summary>
        internal int Port { get; set; }
        /// <summary>
        /// 根目录
        /// </summary>
        internal string Root { get; set; } = "wwwroot";

        /// <summary>
        /// 是否启用静态缓存
        /// </summary>
        internal bool IsStaticsCached { get; set; }

        /// <summary>
        /// 是压启用内容压缩
        /// </summary>
        internal bool IsZiped { get; set; }

        /// <summary>
        /// http处理数据缓存大小
        /// </summary>
        internal int HandleBufferSize { get; set; } = 64 * 1024;

        /// <summary>
        /// 初始化连接对象复用数
        /// </summary>
        internal int MaxConnects { get; set; } = 1000;

        /// <summary>
        /// 禁止访问列表
        /// </summary>
        public List<string> ForbiddenAccessList { get; set; } = new List<string>();

        /// <summary>
        /// 设置禁止访问列表
        /// </summary>
        /// <param name="list"></param>
        public void SetForbiddenAccessList(params string[] list)
        {
            if (list != null)
            {
                foreach (var item in list)
                {
                    if (!string.IsNullOrEmpty(item) && !ForbiddenAccessList.Contains(item))
                    {
                        ForbiddenAccessList.Add(item);
                    }
                }
            }
        }

        /// <summary>
        /// 默认controller/action
        /// </summary>
        public NameValueItem DefaultRoute
        {
            get; set;
        } = new NameValueItem() { Name = "home", Value = "index" };

        /// <summary>
        /// 默认的首页地址
        /// </summary>
        public string HomePage
        {
            get; set;
        } = "index.html";

        /// <summary>
        /// 支持跨域的header参数
        /// </summary>
        public static List<string> CrossHeaders
        {
            get; set;
        } = new List<string>();

        /// <summary>
        /// 是否是测试模式
        /// </summary>
        public bool IsDebug
        {
            get; set;
        } = false;

        public double Timeout
        {
            get; set;
        } = 180;

        public double ConnectTimeout
        {
            get; set;
        } = 3;

    }
}