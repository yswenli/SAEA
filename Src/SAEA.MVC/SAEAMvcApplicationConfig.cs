/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MVC
*文件名： SAEAMvcApplicationConfig
*版本号： v7.0.0.1
*唯一标识：1ed5d381-d7ce-4ea3-b8b5-c32f581ad49f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/12 10:55:31
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/12 10:55:31
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/

namespace SAEA.MVC
{
    /// <summary>
    /// SAEA MVC 应用程序配置类
    /// </summary>
    public class SAEAMvcApplicationConfig
    {
        /// <summary>
        /// 网站根目录
        /// </summary>
        public string Root
        {
            get; set;
        } = "wwwroot";

        /// <summary>
        /// 监听端口
        /// </summary>
        public int Port
        {
            get; set;
        } = 28080;

        /// <summary>
        /// 是否启用静态文件缓存
        /// </summary>
        public bool IsStaticsCached
        {
            get; set;
        } = true;

        /// <summary>
        /// 是否启用压缩
        /// </summary>
        public bool IsZiped
        {
            get; set;
        } = false;

        /// <summary>
        /// 缓冲区大小
        /// </summary>
        public int BufferSize
        {
            get; set;
        } = 64 * 1024;

        /// <summary>
        /// 最大连接数
        /// </summary>
        public int MaxConnect
        {
            get; set;
        } = 1000;

        /// <summary>
        /// 默认页面
        /// </summary>
        public string DefaultPage
        {
            get; set;
        } = "index.html";

        /// <summary>
        /// 是否启用调试模式
        /// </summary>
        public bool IsDebug
        {
            get; set;
        } = false;

        /// <summary>
        /// 控制器命名空间
        /// </summary>
        public string ControllerNameSpace { get; set; }

        /// <summary>
        /// 初始化 SAEA MVC 应用程序配置类的新实例
        /// </summary>
        public SAEAMvcApplicationConfig()
        {

        }

        /// <summary>
        /// 获取默认配置
        /// </summary>
        public static SAEAMvcApplicationConfig Default
        {
            get
            {
                return new SAEAMvcApplicationConfig();
            }
        }
    }
}
