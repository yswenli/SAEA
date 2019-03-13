using SAEA.MVC;
using System.ServiceProcess;

namespace SAEA.Mvc.ServiceTest
{
    public partial class Service1 : ServiceBase
    {
        SAEAMvcApplication mvcApplication;

        SAEAMvcApplicationConfig mvcConfig;

        public Service1()
        {
            InitializeComponent();

            mvcConfig = SAEAMvcApplicationConfigBuilder.Read();

            mvcApplication = new SAEAMvcApplication(mvcConfig);
        }

        protected override void OnStart(string[] args)
        {
            mvcApplication.Start();
        }

        protected override void OnStop()
        {
            mvcApplication.Stop();
        }

        public static void Run()
        {
            Run(new ServiceBase[]
            {
                new Service1()
            });
        }
    }
}
