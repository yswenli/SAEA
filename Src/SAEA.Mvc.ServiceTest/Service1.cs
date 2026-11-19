using SAEA.MVC.Hosting;
using System.ServiceProcess;

namespace SAEA.Mvc.ServiceTest
{
    /// <summary>
    /// 承载 SAEA.Mvc 的 Windows 服务，随服务启停托管 MVC 站点
    /// </summary>
    public partial class Service1 : ServiceBase
    {
        SAEAMvcApplication mvcApplication;

        SAEAMvcApplicationConfig mvcConfig;

        /// <summary>
        /// 读取 MVC 配置并创建站点应用实例，站点在 OnStart 时才真正启动
        /// </summary>
        public Service1()
        {
            InitializeComponent();

            mvcConfig = SAEAMvcApplicationConfigBuilder.Read();

            mvcApplication = new SAEAMvcApplication(mvcConfig);
        }

        /// <summary>
        /// 服务启动时启动 MVC 站点
        /// </summary>
        /// <param name="args">服务启动参数，此处未使用</param>
        protected override void OnStart(string[] args)
        {
            mvcApplication.Start();
        }

        /// <summary>
        /// 服务停止时停止 MVC 站点
        /// </summary>
        protected override void OnStop()
        {
            mvcApplication.Stop();
        }

        /// <summary>
        /// 以独立进程方式运行当前 Windows 服务
        /// </summary>
        public static void Run()
        {
            Run(new ServiceBase[]
            {
                new Service1()
            });
        }
    }
}
