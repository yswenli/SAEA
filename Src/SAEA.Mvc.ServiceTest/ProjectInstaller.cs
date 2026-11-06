using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.Threading.Tasks;

namespace SAEA.Mvc.ServiceTest
{
    /// <summary>
    /// MVC 服务的安装配置，注册本机系统账户运行的 Service1 服务
    /// </summary>
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        /// <summary>
        /// 初始化安装程序并创建服务进程安装器与服务安装器
        /// </summary>
        public ProjectInstaller()
        {
            InitializeComponent();
        }
    }
}
