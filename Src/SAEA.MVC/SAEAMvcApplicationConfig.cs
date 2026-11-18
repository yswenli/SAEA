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
*命名空间：SAEA.MVC
*文件名： SAEAMvcApplicationConfig
*版本号： v26.4.23.1
*唯一标识：41bc0126-6f22-46ff-bd77-b716325c8978
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/01/22 15:08:29
*描述：SAEAMvcApplicationConfig接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/18 14:05:00
*修改人： yswenli
*版本号： v26.11.16.1
*描述：修正 ActionTimeout 注释：默认 0 不再沿用 Timeout，而是不启用超时包装，只有大于 0 才返回 408；WebSocket 入口固定走超时路径
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
        public int MaxConnects
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
        /// 接口超时时间（秒）
        /// </summary>
        public double Timeout
        {
            get; set;
        } = 180;

        /// <summary>
        /// action 执行超时时间（秒），默认 0 表示不启用超时包装：同步 action 在当前线程直接执行，
        /// 异步 action 同步等待完成，阻塞或耗时过长都不会返回 408；大于 0 时才启用超时包装，超时返回 408 并取消令牌。
        /// WebSocket 入口固定走超时路径，未配置时仍以 <see cref="Timeout"/> 兜底
        /// </summary>
        public double ActionTimeout
        {
            get; set;
        } = 0;

        /// <summary>
        /// 连接超时时间（秒）
        /// </summary>
        public double ConnectTimeout
        {
            get; set;
        } = 6;

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